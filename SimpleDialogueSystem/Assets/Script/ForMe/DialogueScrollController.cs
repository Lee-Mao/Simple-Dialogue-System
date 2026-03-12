using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 專門處理對話框長文本的捲動控制器。
/// 支援打字機效果時自動追蹤行數捲動、滑鼠點擊判定與長按過濾。
/// </summary>
public class DialogueScrollController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("UI References")]
    [SerializeField] private TMP_Text _dialogText;
    [SerializeField] private RectTransform _content;
    [SerializeField] private RectTransform _viewport;

    // 注意：若你的專案沒有 DialogScrollView 類別，請將此處改為標準 ScrollRect
    [SerializeField] private ScrollRect _scrollRect;

    [Header("Settings")]
    [Tooltip("對話框內顯示超過幾行時才啟動捲動功能")]
    [SerializeField] private int _maxVisibleLines = 3;
    [Tooltip("判定為長按的閾值時間(秒)")]
    [SerializeField] private float _holdThreshold = 0.3f;

    [Header("Events")]
    public Action OnClickNextDialog;

    private bool _isHolding;
    private bool _isDetectingHold;
    private int _lastVisibleLine = 0;

    /// <summary>
    /// 同步對話內容的捲動位置。通常在打字機效果每顯示一個字時呼叫。
    /// 使用 Async 確保 Layout 重繪後才進行計算。
    /// </summary>
    public async void PlayDialogAsync()
    {
        // 延遲一個 Frame 確保 TMP_Text 已更新字元資訊
        await Task.Yield();

        if (_dialogText == null || _content == null || _viewport == null || string.IsNullOrEmpty(_dialogText.text))
        {
            return;
        }

        // 取得當前「實際上已顯示」的文字行數
        int visibleLines = GetVisibleLineCount(_dialogText);
        var needScroll = visibleLines > _maxVisibleLines;

        // 僅在文字超過框體時啟動捲動條功能
        _scrollRect.enabled = needScroll;

        if (needScroll)
        {
            // 強制立即重新計算 Layout，確保 content 高度正確
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

            // 若目前打字進度產生了新的一行，則向下捲動
            if (visibleLines > _lastVisibleLine)
            {
                float lineHeight = GetLineHeight(_dialogText);

                Vector2 pos = _content.anchoredPosition;
                // 向下偏移一行高度，但不超過最大可捲動範圍
                pos.y = Mathf.Min(pos.y + lineHeight, GetMaxScrollPosition());
                _content.anchoredPosition = pos;

                _lastVisibleLine = visibleLines;
            }
        }
        else
        {
            // 文字較短時，重置 content 位置至頂部
            _content.anchoredPosition = Vector2.zero;
            _lastVisibleLine = visibleLines;
        }
    }

    /// <summary>
    /// 計算 TextMeshPro 當前 maxVisibleCharacters 所涵蓋的實際行數
    /// </summary>
    private int GetVisibleLineCount(TMP_Text text)
    {
        text.ForceMeshUpdate();

        var charInfos = text.textInfo.characterInfo;
        int visibleCount = Mathf.Min(text.maxVisibleCharacters, text.textInfo.characterCount);

        if (visibleCount <= 0) return 0;

        int lastVisibleLine = 0;
        for (int i = 0; i < visibleCount; i++)
        {
            // 忽略不可見字元（如空白或換行符號本身）
            if (!charInfos[i].isVisible) continue;
            lastVisibleLine = Mathf.Max(lastVisibleLine, charInfos[i].lineNumber);
        }

        // lineNumber 是以 0 為基底，故加 1 轉為總行數
        return lastVisibleLine + 1;
    }

    private float GetLineHeight(TMP_Text text)
    {
        text.ForceMeshUpdate();
        if (text.textInfo.lineCount == 0) return 0;
        return text.textInfo.lineInfo[0].lineHeight;
    }

    private float GetMaxScrollPosition()
    {
        float contentHeight = _content.rect.height;
        float viewportHeight = _viewport.rect.height;
        return Mathf.Max(0, contentHeight - viewportHeight);
    }

    #region Click / Hold detection (點擊與長按判定)

    public void OnPointerDown(PointerEventData eventData)
    {
        _isHolding = false;
        if (!_isDetectingHold)
        {
            // 開始異步偵測長按
            _ = HoldDetectionAsync();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        OnPointerUpEvent();
    }

    /// <summary>
    /// 處理滑鼠放開後的邏輯，區分單純點擊、拖拽與長按。
    /// </summary>
    public void OnPointerUpEvent()
    {
        // 若玩家是在拖拽捲動條，則不視為點擊下一則對話
        // 注意：這裡假設你使用的 ScrollRect 有 IsDragging 屬性，若無可替換為自定義判定
        // if (_scrollRect.velocity.magnitude > 0.1f) ... 

        _isDetectingHold = false;

        // 只有在非長按且非拖拽的情況下，才觸發「下一句」事件
        if (!_isHolding)
        {
            OnClickNextDialog?.Invoke();
        }

        _isHolding = false;
    }

    /// <summary>
    /// 使用非同步方式實作長按偵測，避免使用協程 (Coroutine) 產生的額外負擔。
    /// </summary>
    public async Task HoldDetectionAsync()
    {
        _isDetectingHold = true;
        var elapsed = 0f;

        while (_isDetectingHold && elapsed < _holdThreshold)
        {
            elapsed += Time.deltaTime;
            await Task.Yield();
        }

        // 若時間結束時玩家仍未放開按鍵，則判定為長按
        if (_isDetectingHold)
        {
            _isHolding = true;
        }
    }
    #endregion
}