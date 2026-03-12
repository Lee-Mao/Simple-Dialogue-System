using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 模組化對話控制中心
/// 採用 Task-based 異步架構，整合文字打字機、自動播放、以及視覺適配器。
/// </summary>
public class ModularDialogueController : MonoBehaviour
{
    [System.Serializable]
    public class DialogueContent
    {
        public string CharacterName;
        [TextArea(3, 10)] public string Content;
        public Sprite Portrait;
    }

    public enum DialogueState { Idle, Typing, Waiting, Transition }

    [Header("UI Components")]
    [SerializeField] private GameObject _speechPanel;
    [SerializeField] private TMP_Text _characterNameText;
    [SerializeField] private TMP_Text _dialogText;
    [SerializeField] private Image _characterPortraitImage;

    [Header("Next & Auto Features")]
    [SerializeField] private GameObject _nextTipImage; // 提示玩家可以點擊下一則的圖示
    [Tooltip("對應 AutoPlay 階層的延遲秒數 (0階為關閉)")]
    [SerializeField] private float[] _autoPlayDelayStages = { 0f, 2f, 1.5f, 1f };

    [Header("Modules")]
    [SerializeField] public DialogueScrollController DialogueScrollController;

    [Header("Settings")]
    [SerializeField] private int _autoPlayLevel = 0;

    // 狀態追蹤與資料隊列
    private DialogueState _currentState = DialogueState.Idle;
    private List<DialogueContent> _dialogueQueue = new List<DialogueContent>();
    private int _currentIndex = 0;

    // 異步任務控制：用於精確中斷打字效果與自動播放計時
    private CancellationTokenSource _typingCTS;
    private CancellationTokenSource _autoPlayCTS;

    public UnityEngine.Events.UnityEvent OnDialogueEnd;

    // --- 流程控制 ---

    /// <summary>
    /// 進入對話序列的主入口
    /// </summary>
    public async void StartDialogue(List<DialogueContent> contents)
    {
        _dialogueQueue = new List<DialogueContent>(contents);
        _currentIndex = 0;
        _speechPanel.SetActive(true);

        // 綁定 ScrollView 的點擊事件回調至控制器的輸入處理
        if (DialogueScrollController != null)
            DialogueScrollController.OnClickNextDialog = HandleInputClick;

        await ShowCurrentSentence();
    }

    /// <summary>
    /// 顯示當前索引的對話內容，並處理打字與自動播放邏輯
    /// </summary>
    private async Task ShowCurrentSentence()
    {
        if (_currentIndex >= _dialogueQueue.Count)
        {
            EndDialogue();
            return;
        }

        var data = _dialogueQueue[_currentIndex];
        _characterNameText.text = data.CharacterName;
        _characterPortraitImage.sprite = data.Portrait;
        _characterPortraitImage.gameObject.SetActive(data.Portrait != null);

        // 準備打字階段
        _nextTipImage.SetActive(false);
        _currentState = DialogueState.Typing;

        _typingCTS = new CancellationTokenSource();
        try
        {
            await TypeTextEffect(data.Content, _typingCTS.Token);
        }
        catch (TaskCanceledException) { /* 正常中斷，不視為錯誤 */ }
        finally
        {
            _typingCTS?.Dispose();
            _typingCTS = null;
        }

        if (!Application.isPlaying) return;

        // 進入等待點擊階段
        _currentState = DialogueState.Waiting;
        _nextTipImage.SetActive(true);

        // 若開啟自動播放，啟動計時任務
        if (_autoPlayLevel > 0)
        {
            await TriggerAutoPlay();
        }
    }

    /// <summary>
    /// 逐字顯示效果 (打字機)
    /// </summary>
    private async Task TypeTextEffect(string text, CancellationToken token)
    {
        _dialogText.text = text;
        _dialogText.maxVisibleCharacters = 0;
        _dialogText.ForceMeshUpdate();
        int totalChars = _dialogText.textInfo.characterCount;

        for (int i = 0; i <= totalChars; i++)
        {
            if (token.IsCancellationRequested)
            {
                // 中斷時立即顯示全文字
                _dialogText.maxVisibleCharacters = totalChars;
                return;
            }
            _dialogText.maxVisibleCharacters = i;

            // 每次打字更新捲動視窗位置，確保最新文字可見
            if (DialogueScrollController != null) DialogueScrollController.PlayDialogAsync();

            await Task.Delay(50, token);
        }
    }

    // --- 交互控制 (由 Tester 或 UI 觸發) ---

    /// <summary>
    /// 統一處理玩家的「前進」指令 (點擊或按鍵)
    /// </summary>
    public void HandleInputClick()
    {
        if (_currentState == DialogueState.Typing)
        {
            // 打字中點擊：取消打字 Task，直接顯示完整文字
            _typingCTS?.Cancel();
        }
        else if (_currentState == DialogueState.Waiting)
        {
            // 等待中點擊：切換至下一句
            MoveToNext();
        }
    }

    /// <summary>
    /// 處理自動播放的延遲等待
    /// </summary>
    private async Task TriggerAutoPlay()
    {
        _autoPlayCTS?.Cancel(); // 確保不會有重複的計時任務
        _autoPlayCTS = new CancellationTokenSource();
        try
        {
            float delay = _autoPlayDelayStages[_autoPlayLevel];
            await Task.Delay((int)(delay * 1000), _autoPlayCTS.Token);

            // 時間到且狀態未改變時，自動前往下一句
            if (_currentState == DialogueState.Waiting) MoveToNext();
        }
        catch (TaskCanceledException) { }
    }

    private void MoveToNext()
    {
        _autoPlayCTS?.Cancel();
        _currentIndex++;
        ShowCurrentSentence();
    }

    /// <summary>
    /// 切換自動播放階層 (可由外部 UI 切換按鈕呼叫)
    /// </summary>
    public void SetAutoPlayLevel(int level)
    {
        _autoPlayLevel = Mathf.Clamp(level, 0, _autoPlayDelayStages.Length - 1);
        if (_autoPlayLevel > 0 && _currentState == DialogueState.Waiting)
            TriggerAutoPlay();
    }

    /// <summary>
    /// 強制終止對話流程
    /// </summary>
    public void SkipDialogue()
    {
        // 先中斷所有執行中的異步任務，避免洩漏
        _typingCTS?.Cancel();
        _autoPlayCTS?.Cancel();

        _dialogueQueue.Clear();
        _currentIndex = 0;
        EndDialogue();
    }

    private void EndDialogue()
    {
        _speechPanel.SetActive(false);
        _currentState = DialogueState.Idle;

        // 發送事件，通知外部系統（如測試器）重置狀態
        OnDialogueEnd?.Invoke();
    }
}