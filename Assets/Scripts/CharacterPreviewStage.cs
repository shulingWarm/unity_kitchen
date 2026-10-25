using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RollABall
{
    /// <summary>
    /// 主菜单"选择角色"的 3D 预览：每个候选形象独占一个方形展位
    /// （独立相机 + RenderTexture + 独立拖拽旋转），互不干扰。
    /// </summary>
    public class CharacterPreviewStage : MonoBehaviour
    {
        public GameObject[] prefabs;             // 候选形象（顺序 = 展位顺序）
        public Color backgroundColor = new Color(0.07f, 0.09f, 0.13f);
        public float modelHeight = 1.5f;         // 展示高度（米）
        public Texture2D chibiTexture;           // 豆豆男孩 basecolor
        public Texture2D catTexture;             // 小猫 basecolor
        public Material catMaterial;             // 小猫成品材质（条纹贴图）

        private class Cell
        {
            public Camera cam;
            public RenderTexture rt;
            public Transform model;
            public GameObject rawGO;
        }

        private readonly List<Cell> _cells = new();
        private Light _light;                    // 整个预览台一盏；不回收会越叠越亮

        /// <summary>搭建展位，返回每个展位的 RawImage（供 UI 横排布局）。</summary>
        public RawImage[] BuildCells(Transform uiParent, Vector2 cellSize)
        {
            ClearCells();

            var result = new RawImage[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                int index = i;
                float originX = i * 10f;   // 每个展位独占 10 米宽的世界区域，相机互不入镜
                var rt = new RenderTexture(512, 512, 24);

                // 相机
                var camGO = new GameObject($"PreviewCam_{i}", typeof(Camera));
                camGO.transform.SetParent(transform, false);
                var cam = camGO.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backgroundColor;
                cam.targetTexture = rt;
                cam.fieldOfView = 40f;
                cam.transform.position = new Vector3(originX, 0.9f, -2.6f);
                cam.transform.rotation = Quaternion.Euler(5f, 0f, 0f);

                // 灯光（整个预览台一盏；反复开关面板不再叠加变亮）
                if (_light == null)
                {
                    var lightGO = new GameObject("PreviewLight", typeof(Light));
                    lightGO.transform.SetParent(transform, false);
                    _light = lightGO.GetComponent<Light>();
                    _light.type = LightType.Directional;
                    _light.intensity = 1.1f;
                    _light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                }

                // 模型（归一化高度、朝向相机、脚底贴 y=0、居中于本展位）
                var model = Instantiate(prefabs[i], transform);
                model.name = $"PreviewModel_{i}";
                float yaw = i == 1 ? 0f : 180f;   // 绑骨猫(i=1)原生面向镜头，其余 Mixamo 模型需转 180°
                model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var b = renderers[0].bounds;
                    for (int k = 1; k < renderers.Length; k++) b.Encapsulate(renderers[k].bounds);
                    if (b.size.y > 0.0001f)
                    {
                        model.transform.localScale = Vector3.one * (modelHeight / b.size.y);
                        b = renderers[0].bounds;
                        for (int k = 1; k < renderers.Length; k++) b.Encapsulate(renderers[k].bounds);
                    }
                    model.transform.position += new Vector3(originX - b.center.x, -b.min.y, -b.center.z);

                    // 材质接线：豆豆男孩补 basecolor；小猫套成品材质（prefab 原生材质无贴图）；默认模型用原生材质
                    if (i == 0 && chibiTexture != null)
                    {
                        var mat = new Material(Shader.Find("Standard")) { mainTexture = chibiTexture };
                        foreach (var r in renderers) r.sharedMaterial = mat;
                    }
                    else if (i == 1 && catMaterial != null)
                    {
                        foreach (var r in renderers) r.sharedMaterial = catMaterial;
                    }
                }

                _cells.Add(new Cell { cam = cam, rt = rt, model = model.transform });

                // RawImage（带拖拽转发）
                var rawGO = new GameObject($"PreviewImage_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(CellDrag));
                rawGO.transform.SetParent(uiParent, false);
                var rawRt = (RectTransform)rawGO.transform;
                rawRt.anchorMin = rawRt.anchorMax = rawRt.pivot = new Vector2(0.5f, 0.5f);
                rawRt.sizeDelta = cellSize;
                var raw = rawGO.GetComponent<RawImage>();
                raw.texture = rt;
                var drag = rawGO.GetComponent<CellDrag>();
                drag.stage = this;
                drag.index = index;
                result[i] = raw;
            }
            return result;
        }

        private void ClearCells()
        {
            if (_light != null) Destroy(_light.gameObject);   // 灯光一并回收，防止叠加变亮
            foreach (var c in _cells)
            {
                if (c.cam != null) Destroy(c.cam.gameObject);
                if (c.model != null) Destroy(c.model.gameObject);
                if (c.rawGO != null) Destroy(c.rawGO);
                if (c.rt != null) c.rt.Release();
            }
            _cells.Clear();
        }

        /// <summary>拖动某个展位：只旋转该展位的模型（水平位移 → 绕竖直轴）。</summary>
        public void RotateCell(int index, float deltaX)
        {
            if (index < 0 || index >= _cells.Count) return;
            var model = _cells[index].model;
            if (model != null) model.Rotate(0f, deltaX * 0.4f, 0f, Space.World);
        }

        public void SetVisible(bool on)
        {
            foreach (var c in _cells)
            {
                if (c.cam != null) c.cam.enabled = on;
                if (c.model != null) c.model.gameObject.SetActive(on);
                if (c.rawGO != null) c.rawGO.SetActive(on);
            }
        }

        /// <summary>拖拽转发：水平位移驱动对应展位的模型旋转（挂在 RawImage 同节点）。</summary>
        private class CellDrag : MonoBehaviour, IDragHandler
        {
            public CharacterPreviewStage stage;
            public int index;

            public void OnDrag(PointerEventData e)
            {
                if (stage != null) stage.RotateCell(index, e.delta.x);
            }
        }
    }
}
