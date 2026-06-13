using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单 UI 逻辑。自动查找 StartButton 并绑定点击事件。
/// 使用 RuntimeInitializeOnLoadMethod 确保在场景加载后自动初始化。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu") return;

        // 查找 Canvas，如果不存在则创建
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 添加 MainMenuUI 组件
        if (canvas.GetComponent<MainMenuUI>() == null)
        {
            canvas.gameObject.AddComponent<MainMenuUI>();
        }
    }

    void Start()
    {
        // 自动查找场景中名为 "StartButton" 的按钮
        GameObject btnGo = GameObject.Find("StartButton");
        if (btnGo != null)
        {
            Button btn = btnGo.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(OnStartGame);
            }
        }
    }

    public void OnStartGame()
    {
        SceneManager.LoadScene("GameMap");
    }
}