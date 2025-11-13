using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level condition cho Attack Time mode: có timer, thua khi hết giờ
/// </summary>
public class LevelAttackTime : LevelCondition
{
    private float m_time = 60f;
    private DualBoardGameManager m_dualBoardMngr;

    // Setup với DualBoardGameManager
    public void Setup(float value, Text txt, DualBoardGameManager dualBoardMngr)
    {
        m_txt = txt;
        m_time = value;
        m_dualBoardMngr = dualBoardMngr;

        UpdateText();
    }

    private void Update()
    {
        if (m_conditionCompleted) return;

        // Chỉ countdown khi game đang chạy
        GameManager gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null || gameManager.State != GameManager.eStateGame.GAME_STARTED) return;

        m_time -= Time.deltaTime;

        UpdateText();

        // Hết giờ -> GameOver
        if (m_time <= 0f)
        {
            m_time = 0f;
            UpdateText();
            OnConditionComplete();
        }
    }

    protected override void UpdateText()
    {
        if (m_txt == null) return;

        int minutes = Mathf.FloorToInt(m_time / 60f);
        int seconds = Mathf.FloorToInt(m_time % 60f);

        m_txt.text = string.Format("TIME:\n{0:00}:{1:00}", minutes, seconds);
    }
}
