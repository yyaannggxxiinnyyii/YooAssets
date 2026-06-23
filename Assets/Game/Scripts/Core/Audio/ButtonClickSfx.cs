using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮点击音效组件
/// 挂到任何带 Button 的 GameObject 上，点击时自动播放 ButtonClick 音效
/// 无需手动在每个脚本中调用 PlaySfx
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSfx : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        AudioManager.Instance?.PlaySfx(SoundId.ButtonClick);
    }
}
