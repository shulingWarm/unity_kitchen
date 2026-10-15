using System.Collections.Generic;
using System.IO;
using RollABall;
using UnityEditor;
using UnityEngine;

namespace RollABallEditorTools
{
    /// <summary>
    /// 把工程根 maps/*.txt 解析校验后转成 Assets/Maps/*.asset（MapData）。
    /// 规则：第一行"行数 列数"；网格字符 0/1/x/p；p 最多 1 个（0 个则兜底左上角第一个地面格）。
    /// 资产已存在时覆盖字段（保留 GUID，场景引用不失效）。
    /// </summary>
    public static class MapImporter
    {
        private const string AssetFolder = "Assets/Maps";

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
                    MapData parsed = ParseFile(txtPath);
                    string assetPath = $"{AssetFolder}/{Path.GetFileNameWithoutExtension(txtPath)}.asset";

                    var existing = AssetDatabase.LoadAssetAtPath<MapData>(assetPath);
                    if (existing != null)
                    {
                        existing.rows = parsed.rows;
                        existing.cols = parsed.cols;
                        existing.cellSize = parsed.cellSize;
                        existing.grid = parsed.grid;
                        existing.spawnCell = parsed.spawnCell;
                        EditorUtility.SetDirty(existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(parsed, assetPath);
                    }

                    okCount++;
                    Debug.Log($"[MapImporter] {assetPath} ← {Path.GetFileName(txtPath)}" +
                              $"（{parsed.rows}x{parsed.cols}，出生点 r{parsed.spawnCell.x} c{parsed.spawnCell.y}，" +
                              $"小球 {parsed.CountPickups()} 个）");
                }
                catch (System.Exception e)
                {
                    failCount++;
                    Debug.LogError($"[MapImporter] {Path.GetFileName(txtPath)} 导入失败：{e.Message}");
                }
            }
            AssetDatabase.SaveAssets();
        }

        private static MapData ParseFile(string path)
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
            if (content.Count - 1 < rows)
                throw new System.Exception($"标称 {rows} 行，但文件只有 {content.Count - 1} 行地图数据");

            var grid = new string[rows];
            int pCount = 0;
            var spawn = new Vector2Int(-1, -1);

            for (int r = 0; r < rows; r++)
            {
                string line = content[r + 1];
                if (line.Length != cols)
                    throw new System.Exception($"第 {r + 2} 行长度为 {line.Length}，与标称列数 {cols} 不符");

                var sb = new System.Text.StringBuilder(cols);
                for (int c = 0; c < cols; c++)
                {
                    char ch = char.ToLowerInvariant(line[c]);
                    switch (ch)
                    {
                        case '0':
                        case '1':
                        case 'x':
                            sb.Append(ch);
                            break;
                        case 'p':
                            pCount++;
                            spawn = new Vector2Int(r, c);
                            sb.Append('0');   // 出生点站在地面格上，归一化存储
                            break;
                        default:
                            throw new System.Exception(
                                $"第 {r + 2} 行第 {c + 1} 列出现非法字符 '{line[c]}'（只允许 0/1/x/p）");
                    }
                }
                grid[r] = sb.ToString();
            }

            if (pCount > 1) throw new System.Exception($"p 出现了 {pCount} 次，最多只能有 1 个");
            if (pCount == 0)
            {
                for (int r = 0; r < rows && spawn.x < 0; r++)
                    for (int c = 0; c < cols; c++)
                        if (grid[r][c] == '0') { spawn = new Vector2Int(r, c); break; }
                if (spawn.x < 0) throw new System.Exception("地图里没有地面格（至少需要一个 0 或 p）");
            }

            var asset = ScriptableObject.CreateInstance<MapData>();
            asset.rows = rows;
            asset.cols = cols;
            asset.grid = grid;
            asset.spawnCell = spawn;
            return asset;
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
