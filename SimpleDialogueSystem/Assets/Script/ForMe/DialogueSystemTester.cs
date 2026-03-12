using System.Collections.Generic;
using UnityEngine;
using MyBox;

public class DialogueSystemTester : MonoBehaviour
{
    [Separator("必要的組件引用")]
    [MustBeAssigned]
    [SerializeField] private ModularDialogueController _dialogueController;

    [Separator("測試資料設定")]
    [SerializeField] private List<ModularDialogueController.DialogueContent> _testConversations;

    [SerializeField] private bool _isStarted = false;

    [ButtonMethod]
    public void StartDialogue()
    {
        if (_dialogueController == null) return;

        if (_testConversations == null || _testConversations.Count == 0)
        {
            Debug.LogWarning("測試對話內容為空，請先在 Inspector 填入資料。");
            return;
        }

        _isStarted = true;
        _dialogueController.StartDialogue(_testConversations);
    }

    [ButtonMethod]
    public void NextStep()
    {
        if (_isStarted && _dialogueController != null)
        {
            _dialogueController.HandleInputClick();
        }
        else
        {
            Debug.Log("對話尚未開始，請先點擊 Start Dialogue。");
        }
    }

    /// <summary>
    /// 手動重置狀態的按鈕
    /// </summary>
    [ButtonMethod]
    public void ResetStatus()
    {
        _isStarted = false;
    }
}