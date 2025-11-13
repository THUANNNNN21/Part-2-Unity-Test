using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIPanelMain : MonoBehaviour, IMenu
{
    [SerializeField] private Button btnTimer;

    [SerializeField] private Button btnMoves;
    [SerializeField] private Button btnDualBoard;
    [SerializeField] private Button btnAttackTime; // Attack Time mode button
    [SerializeField] private Button btnAutoWin; // Auto Win button
    [SerializeField] private Button btnAutoLose; // Auto Lose button

    private UIMainManager m_mngr;

    private void Awake()
    {
        btnMoves.onClick.AddListener(OnClickMoves);
        btnTimer.onClick.AddListener(OnClickTimer);
        btnDualBoard.onClick.AddListener(OnClickDualBoard);

        if (btnAttackTime != null)
        {
            btnAttackTime.onClick.AddListener(OnClickAttackTime);
        }

        if (btnAutoWin != null)
        {
            btnAutoWin.onClick.AddListener(OnClickAutoWin);
        }

        if (btnAutoLose != null)
        {
            btnAutoLose.onClick.AddListener(OnClickAutoLose);
        }
    }

    private void OnDestroy()
    {
        if (btnMoves) btnMoves.onClick.RemoveAllListeners();
        if (btnTimer) btnTimer.onClick.RemoveAllListeners();
        if (btnDualBoard) btnDualBoard.onClick.RemoveAllListeners();
        if (btnAttackTime) btnAttackTime.onClick.RemoveAllListeners();
        if (btnAutoWin) btnAutoWin.onClick.RemoveAllListeners();
        if (btnAutoLose) btnAutoLose.onClick.RemoveAllListeners();
    }

    public void Setup(UIMainManager mngr)
    {
        m_mngr = mngr;
    }

    private void OnClickTimer()
    {
        m_mngr.LoadLevelTimer();
    }

    private void OnClickMoves()
    {
        m_mngr.LoadLevelMoves();
    }
    private void OnClickDualBoard()
    {
        m_mngr.LoadLevelDualBoard();
    }

    private void OnClickAttackTime()
    {
        m_mngr.LoadLevelAttackTime();
    }

    private void OnClickAutoWin()
    {
        // Gọi UIMainManager để xử lý (vì UIPanelMain sẽ bị inactive sau khi chuyển scene)
        m_mngr.LoadLevelDualBoardWithAutoWin();
    }

    private void OnClickAutoLose()
    {
        // Gọi UIMainManager để xử lý
        m_mngr.LoadLevelDualBoardWithAutoLose();
    }

    public void Show()
    {
        this.gameObject.SetActive(true);
    }

    public void Hide()
    {
        this.gameObject.SetActive(false);
    }
}
