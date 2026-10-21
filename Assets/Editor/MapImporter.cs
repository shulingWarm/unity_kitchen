using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RollABall;
using UnityEditor;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 把工程根 maps/*.txt 解析校验后转成 Assets/Maps/*.asset（MapData）。
    /// 网格行：单元格用空白（tab/空格）分隔，token 为 0/1/cut/p/wN/s1-s4；
    /// 兼容旧格式：整行无分隔符且长度等于列数时按单字符解析。
    /// 图例区（均在网格行之后，空行随意）：
    ///   「wN:」小节 → image:/model:/object: 三个标签字段（顶图、取出物品 fbx、object.json 物品 id）；
    ///   「tasks:」小节 → 每个任务一个小节「物品id:」+ time:/success:/fail:（限时秒、完成加分、超时扣分）；
    ///   顶层字段 → task_num:（驻留卡数）、goal:（胜利分数）、total_time:（总时长秒）、
    ///   cell_size:（格子边长米，可选，缺省 2）。
    /// 同时把 Assets/GameInfo/object.json 烘焙成 ItemRegistry.asset（id → 图标），
    /// 把全部地图按文件名排序烘焙成 LevelRegistry.asset（关卡顺序）；
    /// 图例里的 object:/tasks: 的 id 必须都登记在内。模型缺贴图时的兜底材质
    /// 优先用其同目录的 colormap.png / 同名 png，否则用图例图片。
    /// 资产已存在时覆盖字段（保留 GUID，场景引用不失效）。
    /// </summary>
    public static class MapImporter
    {
        private const string AssetFolder = "Assets/Maps";
        private const string GameInfoFolder = "Assets/GameInfo";
        private const string ObjectJsonPath = "Assets/GameInfo/object.json";
        private const string ItemRegistryAssetPath = "Assets/GameInfo/ItemRegistry.asset";
        private const string LevelRegistryAssetPath = "Assets/GameInfo/LevelRegistry.asset";

        /// <summary>图例区允许的顶层标签字段（其他小节字段行遇到它们即停止收集）。</summary>
        private static readonly string[] TopLevelFields = { "task_num", "goal", "total_time", "cell_size" };

        private static readonly Regex BoxToken = new Regex(@"^w\d+$");
        private static readonly Regex SectionLine = new Regex(@"^\s*(\w+)\s*:\s*$");   // 小节头：wN: / tasks: / 任务id:
        private static readonly Regex FieldLine = new Regex(@"^\s*(\w+)\s*:\s*(\S.*)$"); // 标签字段：key: value

        private static bool IsTopLevelField(string key) => System.Array.IndexOf(TopLevelFields, key) >= 0;

        [MenuItem("Tools/Roll-a-Ball/3 - 导入 maps/ 下的地图文件")]
        public static void ImportMenu()
        {
            ImportAll(out int ok, out int fail);
            EditorUtility.DisplayDialog("导入地图",
                fail == 0 ? $"成功导入 {ok} 张地图。" : $"成功 {ok} 张，失败 {fail} 张，详情见 Console。", "好的");
        }

        /// <summary>供场景生成器调用：导入全部地图并返回 map1.asset（不存在时为 null）。</summary>
        public static MapData ImportAllAndGetDefault()
        {
            ImportAll(out _, out _);
            return AssetDatabase.LoadAssetAtPath<MapData>(AssetFolder + "/map1.asset");
        }

        public static void ImportAll(out int okCount, out int failCount)
        {
            okCount = 0;
            failCount = 0;

            // 物品注册表是图例 object:/tasks: 校验和任务系统的根，失败则整体不导入
            HashSet<string> itemIds;
            try
            {
                itemIds = BakeItemRegistry();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapImporter] 物品注册表烘焙失败：{e.Message}");
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string srcDir = Path.Combine(projectRoot, "maps");
            if (!Directory.Exists(srcDir))
            {
                Debug.LogError($"[MapImporter] 找不到地图源文件夹：{srcDir}");
                return;
            }
            EnsureFolder(AssetFolder);

            foreach (string txtPath in Directory.GetFiles(srcDir, "*.txt"))
            {
                try
                {
                    MapData parsed = ParseFile(txtPath, itemIds);
                    string assetPath = $"{AssetFolder}/{Path.GetFileNameWithoutExtension(txtPath)}.asset";
                    ApplyToAsset(parsed, assetPath);

                    okCount++;
                    Debug.Log($"[MapImporter] {assetPath} ← {Path.GetFileName(txtPath)}" +
                              $"（{parsed.rows}x{parsed.cols}，出生点 r{parsed.spawnCell.x} c{parsed.spawnCell.y}，" +
                              $"木箱类型 {parsed.blockTypes.Length} 种，任务 {parsed.tasks.Length} 种/驻留 {parsed.taskNum} 张，" +
                              $"目标 {parsed.goal} 分/{parsed.totalTime:0} 秒）");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"[MapImporter] {Path.GetFileName(txtPath)} 导入失败：{e.Message}");
                }
            }

            // 全部地图处理后烘焙关卡列表（按文件名排序 = 关卡顺序）
            try
            {
                BakeLevelRegistry();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MapImporter] 关卡列表烘焙失败：{e.Message}");
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>供场景生成器调用：返回烘焙好的关卡列表（不存在时为 null）。</summary>
        public static LevelRegistry GetBakedLevelRegistry()
            => AssetDatabase.LoadAssetAtPath<LevelRegistry>(LevelRegistryAssetPath);

        // ---------------- 解析 ----------------

        private static MapData ParseFile(string path, HashSet<string> itemIds)
        {
            var content = new List<string>(File.ReadAllLines(path));
            for (int i = 0; i < content.Count; i++) content[i] = content[i].TrimEnd().TrimEnd('\r');
            while (content.Count > 0 && content[^1].Length == 0) content.RemoveAt(content.Count - 1);
            if (content.Count == 0) throw new System.Exception("文件为空");

            // 第一行：行数 列数（容忍任意空白分隔）
            string[] head = content[0].Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            if (head.Length != 2 || !int.TryParse(head[0], out int rows) || !int.TryParse(head[1], out int cols))
                throw new System.Exception("第一行必须是「行数 列数」两个整数");
            if (rows <= 0 || cols <= 0) throw new System.Exception("行列数必须为正整数");

            // 网格行 / 图例行分界：网格恰好 rows 行，其后所有非空行都是图例
            if (content.Count - 1 < rows)
                throw new System.Exception($"标称 {rows} 行，但文件只有 {content.Count - 1} 行地图数据");

            var gridRows = new string[rows];
            var usedBoxTokens = new HashSet<string>();
            int pCount = 0;
            var spawn = new Vector2Int(-1, -1);

            for (int r = 0; r < rows; r++)
            {
                string line = content[r + 1];
                string[] tokens = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                    throw new System.Exception($"第 {r + 2} 行是空行，网格行数不足 {rows}");

                // 旧格式兼容：无分隔符且每字符恰是一格
                if (tokens.Length == 1 && tokens[0].Length == cols)
                    tokens = tokens[0].Select(ch => ch.ToString()).ToArray();
                else if (tokens.Length != cols)
                    throw new System.Exception(
                        $"第 {r + 2} 行拆出 {tokens.Length} 个单元格，与标称列数 {cols} 不符（分隔符是否遗漏？）");

                var sb = new System.Text.StringBuilder();
                for (int c = 0; c < cols; c++)
                {
                    string t = tokens[c].ToLowerInvariant();
                    switch (t)
                    {
                        case "0":
                        case "1":
                        case "s":    // 出餐口简写 = s1（朝上）
                        case "s1":   // 出餐口朝上
                        case "s2":   // 出餐口朝下
                        case "s3":   // 出餐口朝左
                        case "s4":   // 出餐口朝右
                            break;
                        case "cut":  // 切菜板工作站（墙高底座 + 菜板 + 刀）
                            break;
                        case "p":
                            pCount++;
                            spawn = new Vector2Int(r, c);
                            t = "0";   // 出生点站在地面格上，归一化存储
                            break;
                        case "x":
                            throw new System.Exception(
                                $"第 {r + 2} 行第 {c + 1} 格的 'x'（收集物）已随胜利条件移除，请改为 0");
                        default:
                            if (!BoxToken.IsMatch(t))
                                throw new System.Exception(
                                    $"第 {r + 2} 行第 {c + 1} 格 '{tokens[c]}' 非法（只允许 0/1/cut/p/wN/s1-s4）");
                            usedBoxTokens.Add(t);
                            break;
                    }
                    if (c > 0) sb.Append(' ');
                    sb.Append(t);
                }
                gridRows[r] = sb.ToString();
            }

            if (pCount > 1) throw new System.Exception($"p 出现了 {pCount} 次，最多只能有 1 个");
            if (pCount == 0)
            {
                for (int r = 0; r < rows && spawn.x < 0; r++)
                {
                    string[] toks = gridRows[r].Split(' ');
                    for (int c = 0; c < cols; c++)
                        if (toks[c] == "0") { spawn = new Vector2Int(r, c); break; }
                }
                if (spawn.x < 0) throw new System.Exception("地图里没有可站立的地面格（至少需要一个 0 或 p）");
            }

            // ---------------- 图例 / 任务区 ----------------
            var types = new List<string>();
            var images = new List<string>();
            var materials = new List<Material>();
            var models = new List<GameObject>();
            var itemMaterials = new List<Material>();
            var objectIds = new List<string>();
            var taskDefs = new List<MapData.TaskDef>();
            int taskNum = -1;
            int goal = -1;
            float totalTime = -1f;
            float cellSize = 2f;   // 可选字段，缺省沿用 2 米

            // 所有木箱共用的侧面材质（纯色木棕）
            Material sideMat = GetOrCreatePlainMaterial($"{AssetFolder}/BoxSide.mat", new Color(0.55f, 0.36f, 0.20f));

            for (int i = rows + 1; i < content.Count; )
            {
                string line = content[i];
                if (line.Length == 0) { i++; continue; }

                Match section = SectionLine.Match(line);
                if (section.Success)
                {
                    string name = section.Groups[1].Value.ToLowerInvariant();
                    if (BoxToken.IsMatch(name))
                    {
                        // 木箱图例小节：image/model/object 三个标签字段
                        var fields = ParseLabeledFields(content, ref i, name, new[] { "image", "model", "object" });

                        string imagePath = "Assets/" + fields["image"].Replace('\\', '/');
                        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                        if (tex == null)
                            throw new System.Exception($"图例 {name} 的图片不存在：{imagePath}");

                        string modelPath = "Assets/" + fields["model"].Replace('\\', '/');
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                        if (model == null)
                            throw new System.Exception(
                                $"图例 {name} 的模型不存在或尚未被 Unity 导入：{modelPath}（fbx 需放在 Assets 下）");

                        string objectId = fields["object"].ToLowerInvariant();
                        if (!itemIds.Contains(objectId))
                            throw new System.Exception(
                                $"图例 {name} 的 object '{objectId}' 没有在 object.json 里登记" +
                                $"（现有：{string.Join(", ", itemIds)}）");

                        types.Add(name);
                        images.Add(fields["image"]);
                        materials.Add(GetOrCreateTopMaterial($"{AssetFolder}/BoxTop_{name}.mat", tex));
                        models.Add(model);
                        itemMaterials.Add(GetOrCreateTopMaterial(
                            $"{AssetFolder}/BoxItem_{name}.mat", FindItemTexture(modelPath, imagePath)));
                        objectIds.Add(objectId);
                    }
                    else if (name == "tasks")
                    {
                        // 任务池：每个任务一个小节「物品id:」+ time:/success:/fail:
                        var seenTasks = new HashSet<string>();
                        i++;
                        while (i < content.Count)
                        {
                            string idLine = content[i].Trim();
                            if (idLine.Length == 0) { i++; continue; }

                            Match taskHead = SectionLine.Match(idLine);
                            if (!taskHead.Success)
                            {
                                // 顶层字段（task_num/goal/total_time）出现即归还主循环
                                Match maybeTop = FieldLine.Match(idLine);
                                if (maybeTop.Success && IsTopLevelField(maybeTop.Groups[1].Value.ToLowerInvariant())) break;
                                throw new System.Exception(
                                    $"tasks: 第 {i + 1} 行 '{idLine}' 应为任务小节头「物品id:」（如 corn:，" +
                                    "下面跟 time:/success:/fail: 三行）");
                            }

                            string taskId = taskHead.Groups[1].Value.ToLowerInvariant();
                            if (!itemIds.Contains(taskId))
                                throw new System.Exception(
                                    $"tasks: 里的 '{taskId}' 没有在 object.json 里登记" +
                                    $"（现有：{string.Join(", ", itemIds)}）");
                            if (!seenTasks.Add(taskId))
                                throw new System.Exception($"tasks: 里任务 '{taskId}' 重复定义");

                            var fields = ParseLabeledFields(content, ref i, $"tasks/{taskId}",
                                new[] { "time", "success", "fail" });
                            if (!float.TryParse(fields["time"], out float taskTime) || taskTime <= 0f)
                                throw new System.Exception($"任务 {taskId} 的 time 必须是正数，读到 '{fields["time"]}'");
                            if (!int.TryParse(fields["success"], out int succ) || succ < 0)
                                throw new System.Exception($"任务 {taskId} 的 success 必须是非负整数，读到 '{fields["success"]}'");
                            if (!int.TryParse(fields["fail"], out int failPenalty) || failPenalty < 0)
                                throw new System.Exception($"任务 {taskId} 的 fail 必须是非负整数，读到 '{fields["fail"]}'");

                            taskDefs.Add(new MapData.TaskDef
                            {
                                id = taskId,
                                time = taskTime,
                                success = succ,
                                fail = failPenalty,
                            });
                        }
                        if (taskDefs.Count == 0)
                            throw new System.Exception("tasks: 小节为空（至少写一个任务小节，如 corn: + time/success/fail）");
                    }
                    else
                    {
                        throw new System.Exception(
                            $"图例区第 {i + 1} 行 '{line}' 无法识别（小节头只支持 wN: 与 tasks:）");
                    }
                }
                else
                {
                    // 顶层标签字段：task_num / goal / total_time
                    Match field = FieldLine.Match(line);
                    string key = field.Success ? field.Groups[1].Value.ToLowerInvariant() : null;
                    if (!field.Success || !IsTopLevelField(key))
                        throw new System.Exception(
                            $"图例区第 {i + 1} 行 '{line}' 无法识别" +
                            $"（应为「wN:」/「tasks:」小节头或「{string.Join(":", TopLevelFields)}」之一）");
                    string value = field.Groups[2].Value.Trim();
                    if (key == "task_num")
                    {
                        if (!int.TryParse(value, out taskNum) || taskNum <= 0)
                            throw new System.Exception($"task_num 必须是正整数，读到 '{value}'");
                    }
                    else if (key == "goal")
                    {
                        if (!int.TryParse(value, out goal) || goal <= 0)
                            throw new System.Exception($"goal 必须是正整数，读到 '{value}'");
                    }
                    else if (key == "cell_size")
                    {
                        if (!float.TryParse(value, out cellSize) || cellSize <= 0f)
                            throw new System.Exception($"cell_size 必须是正数（每格边长，单位米），读到 '{value}'");
                    }
                    else // total_time
                    {
                        if (!float.TryParse(value, out totalTime) || totalTime <= 0f)
                            throw new System.Exception($"total_time 必须是正数，读到 '{value}'");
                    }
                    i++;
                }
            }

            if (taskNum < 0)
                throw new System.Exception("缺少 task_num:（同时驻留的任务卡数，如 task_num:3）");
            if (goal < 0)
                throw new System.Exception("缺少 goal:（本关胜利分数线，如 goal:100）");
            if (totalTime < 0f)
                throw new System.Exception("缺少 total_time:（本关总时长秒数，如 total_time:300）");

            foreach (string used in usedBoxTokens)
                if (!types.Contains(used))
                    throw new System.Exception($"网格使用了 {used}，但图例区没有它的定义（wN: 小节，含 image/model/object）");

            var asset = ScriptableObject.CreateInstance<MapData>();
            asset.rows = rows;
            asset.cols = cols;
            asset.gridRows = gridRows;
            asset.spawnCell = spawn;
            asset.blockTypes = types.ToArray();
            asset.blockImages = images.ToArray();
            asset.blockMaterials = materials.ToArray();
            asset.blockModels = models.ToArray();
            asset.blockItemMaterials = itemMaterials.ToArray();
            asset.blockObjectIds = objectIds.ToArray();
            asset.blockSideMaterial = sideMat;
            asset.tasks = taskDefs.ToArray();
            asset.taskNum = taskNum;
            asset.goal = goal;
            asset.totalTime = totalTime;
            asset.cellSize = cellSize;
            return asset;
        }

        /// <summary>
        /// 收集一个小节的标签字段（key 必须在 allowedKeys 内，且全部必填），直到下一个小节头「xxx:」、
        /// 顶层字段（task_num/goal/total_time）或文件尾；停止时 i 落在停止行上（由调用方继续处理）。
        /// </summary>
        private static Dictionary<string, string> ParseLabeledFields(
            List<string> content, ref int i, string token, string[] allowedKeys)
        {
            var fields = new Dictionary<string, string>();
            i++;
            while (i < content.Count)
            {
                string line = content[i].Trim();
                if (line.Length == 0) { i++; continue; }
                if (SectionLine.IsMatch(line)) break;

                Match f = FieldLine.Match(line);
                if (f.Success && IsTopLevelField(f.Groups[1].Value.ToLowerInvariant())) break;   // 顶层字段，归还主循环
                if (!f.Success)
                    throw new System.Exception(
                        $"图例 {token} 第 {i + 1} 行 '{line}' 应为「key: value」格式（{string.Join("/", allowedKeys)}）");

                string key = f.Groups[1].Value.ToLowerInvariant();
                if (System.Array.IndexOf(allowedKeys, key) < 0)
                    throw new System.Exception(
                        $"图例 {token} 的字段 '{key}' 无法识别（只支持 {string.Join("/", allowedKeys)}）");
                if (fields.ContainsKey(key))
                    throw new System.Exception($"图例 {token} 的字段 {key} 重复定义");

                fields[key] = f.Groups[2].Value.Trim();
                i++;
            }

            foreach (string required in allowedKeys)
                if (!fields.ContainsKey(required))
                    throw new System.Exception($"图例 {token} 缺少 {required}: 字段");
            return fields;
        }

        // ---------------- 物品注册表 ----------------

        /// <summary>把 object.json 烘焙成 ItemRegistry.asset（运行时按 id 查图标），返回全部物品 id。</summary>
        private static HashSet<string> BakeItemRegistry()
        {
            if (!File.Exists(ObjectJsonPath))
                throw new System.Exception($"找不到 {ObjectJsonPath}（物品注册表源文件）");

            EnsureFolder(GameInfoFolder);
            var items = ParseItemJson(File.ReadAllText(ObjectJsonPath));

            var registry = AssetDatabase.LoadAssetAtPath<ItemRegistry>(ItemRegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<ItemRegistry>();
                AssetDatabase.CreateAsset(registry, ItemRegistryAssetPath);
            }

            var ids = new string[items.Count];
            var icons = new Texture2D[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                ids[i] = items[i].Key;
                string imagePath = "Assets/" + items[i].Value["image"].Replace('\\', '/');
                icons[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                if (icons[i] == null)
                    throw new System.Exception($"object.json 物品 '{ids[i]}' 的图片不存在：{imagePath}");
            }
            registry.ids = ids;
            registry.icons = icons;
            EditorUtility.SetDirty(registry);
            return new HashSet<string>(ids);
        }

        /// <summary>把 Assets/Maps 下全部 MapData 按文件名排序烘焙成 LevelRegistry.asset（决定关卡顺序）。</summary>
        private static void BakeLevelRegistry()
        {
            string[] guids = AssetDatabase.FindAssets("t:MapData", new[] { AssetFolder });
            var maps = new List<MapData>();
            var names = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var map = AssetDatabase.LoadAssetAtPath<MapData>(path);
                if (map == null) continue;
                maps.Add(map);
                names.Add(Path.GetFileNameWithoutExtension(path));
            }
            if (maps.Count == 0)
                throw new System.Exception($"Assets/Maps 下没有可用的地图资产");

            // 按文件名排序：map1 < map2 < map3（Ordinal 保证数字顺序稳定）
            var order = new int[maps.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            System.Array.Sort(order, (a, b) => string.CompareOrdinal(names[a], names[b]));
            var levels = new MapData[maps.Count];
            for (int i = 0; i < levels.Length; i++) levels[i] = maps[order[i]];

            var registry = AssetDatabase.LoadAssetAtPath<LevelRegistry>(LevelRegistryAssetPath);
            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<LevelRegistry>();
                AssetDatabase.CreateAsset(registry, LevelRegistryAssetPath);
            }
            registry.levels = levels;
            EditorUtility.SetDirty(registry);
        }

        /// <summary>
        /// 解析 object.json 的两层字典结构：{"id": {"image": "路径", ...}, ...}。
        /// 只支持字符串值、块内无嵌套（本项目注册表就是这种扁平结构）。
        /// </summary>
        private static List<KeyValuePair<string, Dictionary<string, string>>> ParseItemJson(string json)
        {
            var items = new List<KeyValuePair<string, Dictionary<string, string>>>();
            var seen = new HashSet<string>();
            foreach (Match block in Regex.Matches(json, "\"([^\"]+)\"\\s*:\\s*\\{([^{}]*)\\}"))
            {
                string id = block.Groups[1].Value;
                if (!seen.Add(id))
                    throw new System.Exception($"object.json 里物品 '{id}' 重复定义");

                var fields = new Dictionary<string, string>();
                foreach (Match f in Regex.Matches(block.Groups[2].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]*)\""))
                    fields[f.Groups[1].Value] = f.Groups[2].Value;
                if (!fields.ContainsKey("image"))
                    throw new System.Exception($"object.json 里物品 '{id}' 缺少 image 字段");

                items.Add(new KeyValuePair<string, Dictionary<string, string>>(id, fields));
            }
            if (items.Count == 0)
                throw new System.Exception("object.json 里没有解析到任何物品（应为 {\"id\": {\"image\": \"路径\"}} 结构）");
            return items;
        }

        // ---------------- 材质烘焙 ----------------

        /// <summary>
        /// 食材模型缺贴图时的兜底贴图：优先模型同目录的 colormap.png（低模资源包的惯用命名），
        /// 其次与模型同名的 png，最后才回退图例图片（箱顶贴图）。
        /// </summary>
        private static Texture2D FindItemTexture(string modelPath, string legendTexPath)
        {
            string modelDir = Path.GetDirectoryName(modelPath)!.Replace('\\', '/');
            string modelBase = Path.GetFileNameWithoutExtension(modelPath);
            var candidates = new[]
            {
                $"{modelDir}/colormap.png",
                $"{modelDir}/{modelBase}.png",
            };
            foreach (string path in candidates)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(legendTexPath);
        }

        private static Material GetOrCreateTopMaterial(string matPath, Texture2D topTexture)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = topTexture;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetOrCreatePlainMaterial(string matPath, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard")) { color = color };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        // ---------------- 资产写入 ----------------

        private static void ApplyToAsset(MapData parsed, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<MapData>(assetPath);
            if (existing != null)
            {
                existing.rows = parsed.rows;
                existing.cols = parsed.cols;
                existing.gridRows = parsed.gridRows;
                existing.spawnCell = parsed.spawnCell;
                existing.blockTypes = parsed.blockTypes;
                existing.blockImages = parsed.blockImages;
                existing.blockMaterials = parsed.blockMaterials;
                existing.blockModels = parsed.blockModels;
                existing.blockItemMaterials = parsed.blockItemMaterials;
                existing.blockObjectIds = parsed.blockObjectIds;
                existing.blockSideMaterial = parsed.blockSideMaterial;
                existing.tasks = parsed.tasks;
                existing.taskNum = parsed.taskNum;
                existing.goal = parsed.goal;
                existing.totalTime = parsed.totalTime;
                existing.cellSize = parsed.cellSize;
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(parsed, assetPath);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
