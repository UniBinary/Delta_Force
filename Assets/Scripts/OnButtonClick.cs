using UnityEngine;
using UnityEngine.UI;

public class OnButtonClick : MonoBehaviour
{
    public Button targetButton;

    void Start()
    {
        if (targetButton != null)
            targetButton.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        SceneLoader.Load(SceneLoader.Scene.GameMap);
    }
}
