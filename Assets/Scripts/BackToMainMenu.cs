using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 结果场景（EvcSucceeded / EvcFailed）中"回到主界面"按钮逻辑。
/// 挂载到包含 Button 组件的 GameObject 上，或通过 targetButton 引用指定按钮。
/// </summary>
public class BackToMainMenu : MonoBehaviour
{
    [SerializeField] Button _targetButton;

    void Start()
    {
        if (_targetButton != null)
        {
            _targetButton.onClick.AddListener(GoToMainMenu);
        }
        else
        {
            // 尝试使用自身按钮
            Button selfButton = GetComponent<Button>();
            if (selfButton != null)
                selfButton.onClick.AddListener(GoToMainMenu);
        }
    }

    public void GoToMainMenu()
    {
        Debug.Log("[BackToMainMenu] 返回主界面");

        // 清理 NetworkManager 单例（有 DontDestroyOnLoad），确保下次进入场景
        // 时创建全新实例
        if (Mirror.NetworkManager.singleton != null)
        {
            Destroy(Mirror.NetworkManager.singleton.gameObject);
        }
        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }

        SceneManager.LoadScene("MainMenu");
    }
}
