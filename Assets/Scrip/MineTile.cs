using UnityEngine;
using UnityEngine.UI;

public class MineTile : MonoBehaviour
{
    public Button button;
    public Text label;

    public bool IsRevealed { get; private set; }
    public bool IsBlocked { get; private set; }
	public bool IsMineMarked { get; private set; }

    private MineHuntGameManager gm;
    private Vector2Int pos;

    public void Init(MineHuntGameManager gameManager, Vector2Int position)
    {
        gm = gameManager;
        pos = position;

        if (button == null) button = GetComponent<Button>();
        if (label == null) label = GetComponentInChildren<Text>(true);

        IsRevealed = false;
        IsBlocked = false;

        if (label != null) label.text = "";
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
            button.interactable = true;
        }

        // 텍스트 중앙 고정
        if (label != null)
        {
            var rt = label.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            label.alignment = TextAnchor.MiddleCenter;
        }
		IsMineMarked = false;
    }

    private void OnClick()
    {
   		if (gm == null || gm.IsGameOver) return;
    	if (IsRevealed) return;
    	if (IsBlocked) return;
    	if (!gm.TryReveal(pos, out int dist)) return;
    	// ✅ 지뢰면 M 표시
    	if (dist == -2)
    	{
	        IsRevealed = true;
	        ShowMine();
	        return;
  		  }
  		  // ✅ 그 외: 숫자 or 도달불가(?)
  		IsRevealed = true;
    	SetNumberText(dist);
    	if (button != null) button.interactable = false;
    }
	public void SetNumberText(int dist)
	{
		if (IsMineMarked) return; // ✅ 지뢰 표시는 절대 덮지 않는다
		if (label == null) return;
		label.text = (dist >= 0) ? dist.ToString() : "?";
	}
    public void SetBlocked(bool blocked, string mark = "X")
    {
        IsBlocked = blocked;

        if (label != null)
            label.text = blocked ? mark : "";

        if (button != null)
            button.interactable = !blocked;
    }

    public void ShowMine()
    {
		IsMineMarked = true;
        if (label != null) label.text = "M";
        if (button != null) button.interactable = false;
    }

    public void Lock()
    {
        if (button != null) button.interactable = false;
    }
}
