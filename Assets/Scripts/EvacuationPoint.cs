using Mirror;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 撤离点：玩家靠近后显示绿色进度条 + 10 秒读秒，完成后执行撤离成功逻辑。
/// 挂载于 GameMap 场景中的撤离点 GameObject 上。
/// </summary>
public class EvacuationPoint : NetworkBehaviour
{
    [Header("撤离读秒时长（秒）")]
    public float evacuationTime = 10f;

    [Header("触发半径")]
    public float triggerRadius = 0.8f;

    // 进度条 UI（屏幕上方）
    private Image _progressBarFill;
    private TextMeshProUGUI _countdownText;
    private GameObject _evacuationUI; // 进度条父物体

    // 当前读秒协程
    private Coroutine _countdownCoroutine;
    private float _currentProgress = 0f;

    // 是否有本地玩家在触发区内
    private bool _localPlayerInside = false;

    void Start()
    {
        // 确保有触发碰撞体
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col == null)
        {
            col = gameObject.AddComponent<CircleCollider2D>();
        }
        col.radius = triggerRadius;
        col.isTrigger = true;

        // 添加绿色指示器
        SetupIndicator();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        // 客户端查找 UI 引用
        FindUIReferences();
    }

    void FindUIReferences()
    {
        // GameObject.Find 找不到 inactive 对象，改用 Canvas Transform.Find
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) return;

        Transform evacUITrans = canvasObj.transform.Find("EvacuationUI");
        if (evacUITrans != null)
        {
            _evacuationUI = evacUITrans.gameObject;

            Transform fillTrans = evacUITrans.Find("EvacuationProgressBg");
            if (fillTrans != null)
                _progressBarFill = fillTrans.GetComponent<Image>();

            Transform countdownTrans = evacUITrans.Find("EvacuationCountdownText");
            if (countdownTrans != null)
                _countdownText = countdownTrans.GetComponent<TextMeshProUGUI>();

            // 初始隐藏
            _evacuationUI.SetActive(false);
        }
    }

    void SetupIndicator()
    {
        // 添加绿色圆形指示器 Sprite
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            sr = gameObject.AddComponent<SpriteRenderer>();
        }
        sr.color = new Color(0.1f, 0.9f, 0.2f, 0.7f);
        sr.sortingOrder = 5;

        // 创建一个圆形 sprite（使用内置方法）
        // 如果项目中有简单的圆形素材就用，否则创建一个
        Sprite circleSprite = Resources.Load<Sprite>("Circle");
        if (circleSprite == null)
        {
            // 使用 Unity 内置的白色方块，通过 Transform scale 和颜色来模拟
            Texture2D tex = new Texture2D(128, 128);
            Color[] pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    float dx = (x - 64f) / 64f;
                    float dy = (y - 64f) / 64f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * 128 + x] = dist <= 1f ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        }
        sr.sprite = circleSprite;
        // 128px texture at 100 px/unit = 1.28 unit diameter = 0.64 unit radius
        // Scale needed to get visual radius = triggerRadius
        sr.transform.localScale = Vector3.one * (triggerRadius / 0.64f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null && player.isLocalPlayer && !player.isSpectating)
        {
            _localPlayerInside = true;
            Debug.Log("[EvacuationPoint] 玩家进入撤离区域");

            // 查找 UI（可能在 Start 时还没创建好）
            if (_evacuationUI == null) FindUIReferences();

            // 显示进度条 UI
            if (_evacuationUI != null)
                _evacuationUI.SetActive(true);

            // 重置进度条
            _currentProgress = 0f;
            if (_progressBarFill != null)
                _progressBarFill.fillAmount = 0f;

            // 开始读秒
            if (_countdownCoroutine != null)
                StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = StartCoroutine(CountdownRoutine());
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        Player player = other.GetComponent<Player>();
        if (player != null && player.isLocalPlayer)
        {
            _localPlayerInside = false;
            Debug.Log("[EvacuationPoint] 玩家离开撤离区域");

            // 取消读秒，隐藏 UI
            if (_countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }

            if (_evacuationUI != null)
                _evacuationUI.SetActive(false);

            _currentProgress = 0f;
            UpdateProgressUI(0f, evacuationTime);
        }
    }

    IEnumerator CountdownRoutine()
    {
        float elapsed = 0f;

        while (elapsed < evacuationTime)
        {
            if (!_localPlayerInside)
            {
                // 玩家已离开，退出
                yield break;
            }

            elapsed += Time.deltaTime;
            float remaining = evacuationTime - elapsed;
            _currentProgress = elapsed / evacuationTime;
            UpdateProgressUI(remaining, evacuationTime);

            yield return null;
        }

        // 读秒完成 — 执行撤离
        Debug.Log("[EvacuationPoint] 撤离读秒完成！执行撤离操作");

        // 隐藏 UI
        if (_evacuationUI != null)
            _evacuationUI.SetActive(false);

        // 通知服务端：本玩家撤离成功
        CmdEvacuate();
    }

    void UpdateProgressUI(float remainingSeconds, float totalSeconds)
    {
        if (_progressBarFill != null)
        {
            _progressBarFill.fillAmount = _currentProgress;
        }
        if (_countdownText != null)
        {
            int seconds = Mathf.CeilToInt(remainingSeconds);
            _countdownText.text = $"{seconds}";
        }
    }

    // ============================================================
    // 服务端：处理玩家撤离成功
    // ============================================================
    [Command(requiresAuthority = false)]
    void CmdEvacuate(NetworkConnectionToClient sender = null)
    {
        // 找到发送命令的玩家
        if (sender == null) return;

        Player player = sender.identity?.GetComponent<Player>();
        if (player == null)
        {
            Debug.LogError("[EvacuationPoint] 找不到发起撤离的玩家");
            return;
        }

        if (player.health <= 0)
        {
            Debug.LogWarning("[EvacuationPoint] 玩家已死亡，无法撤离");
            return;
        }

        GameManager.Instance.OnPlayerEvacuated(player);
    }
}
