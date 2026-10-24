using UnityEngine;

namespace RollABall
{
    /// <summary>音效键位（与 SfxPlayer.clips 下标一一对应，补丁按文件名注入）。</summary>
    public enum SfxKey
    {
        Grab,        // 抓取/捡起
        Place,       // 摆上台面/放下
        Throw,       // 投掷
        Chop,        // 切菜节奏声（按住期间按间隔连放）
        ChopDone,    // 切配完成
        ServeHit,    // 交付命中任务
        ServeMiss,   // 交付白交
        TaskExpired, // 任务卡超时作废
        Tick,        // 倒计时最后 10 秒滴答
        Win,         // 胜利
        Lose,        // 失败
    }

    /// <summary>
    /// 全局音效播放器：场景里一个（由菜单 12 创建并注入 clips），跨场景常驻。
    /// 各系统通过静态 Play(...) 触发；clips 缺位时静默跳过，不影响玩法。
    /// 切菜的"笃笃笃"由 SetChopping 开关 + Update 按固定间隔连放 Chop 音实现。
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        /// <summary>切菜节奏声的间隔（秒），近似 chop 动画的落刀频率。</summary>
        private const float ChopInterval = 0.45f;

        public static SfxPlayer Instance { get; private set; }

        [Tooltip("音效片段数组（下标 = SfxKey）")]
        public AudioClip[] clips;

        private AudioSource source;
        private bool chopping;
        private float nextChopTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);   // 场景重载/主菜单切换时保留先到的
                return;
            }
            Instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!chopping) return;
            if (Time.time < nextChopTime) return;
            Play(SfxKey.Chop, 0.85f, 1f, 0.06f);   // 轻微随机音高，避免机械感
            nextChopTime = Time.time + ChopInterval;
        }

        /// <summary>播放一枚音效；jitter > 0 时音高随机偏移 ±jitter。</summary>
        public static void Play(SfxKey key, float volume = 1f, float pitch = 1f, float jitter = 0f)
        {
            if (Instance == null || Instance.clips == null) return;
            int index = (int)key;
            if (index < 0 || index >= Instance.clips.Length || Instance.clips[index] == null) return;

            Instance.source.pitch = jitter > 0f ? pitch + Random.Range(-jitter, jitter) : pitch;
            Instance.source.PlayOneShot(Instance.clips[index], volume);
        }

        /// <summary>切菜节奏声开关（PlayerController 在切菜状态切换时调用）。</summary>
        public static void SetChopping(bool on)
        {
            if (Instance == null) return;
            if (on && !Instance.chopping) Instance.nextChopTime = Time.time + 0.1f;   // 开切立刻来第一声
            if (!on) Instance.chopping = false;
            else Instance.chopping = true;
        }
    }
}
