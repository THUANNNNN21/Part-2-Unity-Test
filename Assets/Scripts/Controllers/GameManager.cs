using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action<eStateGame> StateChangedAction = delegate { };

    public enum eLevelMode
    {
        TIMER,
        MOVES,
        DUAL_BOARD,      // Mode mới
        ATTACK_TIME      // Mode Attack Time: có timer, remove được item từ playing board
    }

    public enum eStateGame
    {
        SETUP,
        MAIN_MENU,
        GAME_STARTED,
        PAUSE,
        GAME_WIN,   // State mới: WIN
        GAME_OVER,
    }

    private eStateGame m_state;
    public eStateGame State
    {
        get { return m_state; }
        private set
        {
            m_state = value;

            StateChangedAction(m_state);
        }
    }


    private GameSettings m_gameSettings;

    private BoardController m_boardController;
    private DualBoardGameManager m_dualBoardManager; // Thêm reference
    private UIMainManager m_uiMenu;

    private LevelCondition m_levelCondition;
    private eLevelMode m_currentGameplayMode;

    private void Awake()
    {
        State = eStateGame.SETUP;

        m_gameSettings = Resources.Load<GameSettings>(Constants.GAME_SETTINGS_PATH);

        m_uiMenu = FindFirstObjectByType<UIMainManager>();
        m_uiMenu.Setup(this);
    }

    void Start()
    {
        State = eStateGame.MAIN_MENU;
    }

    // Update is called once per frame
    void Update()
    {
        if (m_boardController != null)
            m_boardController.Update();
        else if (m_dualBoardManager != null)
            m_dualBoardManager.Update();
    }


    internal void SetState(eStateGame state)
    {
        State = state;

        if (State == eStateGame.PAUSE)
        {
            DOTween.PauseAll();
        }
        else
        {
            DOTween.PlayAll();
        }
    }

    public void LoadLevel(eLevelMode mode)
    {
        if (mode == eLevelMode.DUAL_BOARD)
        {
            LoadDualBoardGameplay();
            return;
        }

        if (mode == eLevelMode.ATTACK_TIME)
        {
            LoadAttackTimeGameplay();
            return;
        }

        // Classic gameplay
        m_boardController = new GameObject("BoardController").AddComponent<BoardController>();
        m_boardController.StartGame(this, m_gameSettings);

        if (mode == eLevelMode.MOVES)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelMoves>();
            m_levelCondition.Setup(m_gameSettings.LevelMoves, m_uiMenu.GetLevelConditionView(), m_boardController);
        }
        else if (mode == eLevelMode.TIMER)
        {
            m_levelCondition = this.gameObject.AddComponent<LevelTime>();
            m_levelCondition.Setup(m_gameSettings.LevelTime, m_uiMenu.GetLevelConditionView(), this);
        }

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent += GameOver;
        }

        State = eStateGame.GAME_STARTED;
    }

    private void LoadAttackTimeGameplay()
    {
        // Tạo DualBoardGameManager với Attack Time mode
        GameObject attackTimeObj = new GameObject("AttackTimeGameManager");
        m_dualBoardManager = attackTimeObj.AddComponent<DualBoardGameManager>();

        // 🔧 FIX: Set Attack Time mode TRƯỚC khi gọi StartGame
        m_dualBoardManager.SetAttackTimeMode(true);

        // Gọi StartGame (SetupBoards sẽ dùng IsAttackTimeMode = true)
        m_dualBoardManager.StartGame(this, m_gameSettings);

        // Tạo Level Condition với timer
        m_levelCondition = this.gameObject.AddComponent<LevelAttackTime>();
        m_levelCondition.Setup(m_gameSettings.LevelTime, m_uiMenu.GetLevelConditionView(), m_dualBoardManager);

        // Subscribe GameOver event khi hết giờ
        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent += GameOver;
        }

        State = eStateGame.GAME_STARTED;
    }

    private void LoadDualBoardGameplay()
    {
        // Tạo DualBoardGameManager
        GameObject dualBoardObj = new GameObject("DualBoardGameManager");
        m_dualBoardManager = dualBoardObj.AddComponent<DualBoardGameManager>();

        // Gọi StartGame giống như BoardController
        m_dualBoardManager.StartGame(this, m_gameSettings);

        // Dual board gameplay không cần level condition
        // Có thể thêm logic thắng/thua riêng trong DualBoardGameManager

        State = eStateGame.GAME_STARTED;
    }

    public void GameWin()
    {
        StartCoroutine(WaitBoardControllerForWin());
    }

    public void GameOver()
    {
        StartCoroutine(WaitBoardController());
    }

    internal void ClearLevel()
    {
        if (m_boardController)
        {
            m_boardController.Clear();
            Destroy(m_boardController.gameObject);
            m_boardController = null;
        }

        if (m_dualBoardManager)
        {
            m_dualBoardManager.Clear();
            Destroy(m_dualBoardManager.gameObject);
            m_dualBoardManager = null;
        }
    }

    private IEnumerator WaitBoardController()
    {
        // Đợi board xử lý xong
        if (m_boardController != null)
        {
            while (m_boardController.IsBusy)
            {
                yield return new WaitForEndOfFrame();
            }
        }
        else if (m_dualBoardManager != null)
        {
            while (m_dualBoardManager.IsBusy)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        yield return new WaitForSeconds(1f);

        State = eStateGame.GAME_OVER;

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameOver;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }

    private IEnumerator WaitBoardControllerForWin()
    {
        // Đợi board xử lý xong
        if (m_boardController != null)
        {
            while (m_boardController.IsBusy)
            {
                yield return new WaitForEndOfFrame();
            }
        }
        else if (m_dualBoardManager != null)
        {
            while (m_dualBoardManager.IsBusy)
            {
                yield return new WaitForEndOfFrame();
            }
        }

        yield return new WaitForSeconds(1f);

        State = eStateGame.GAME_WIN; // Set state WIN thay vì GAME_OVER

        if (m_levelCondition != null)
        {
            m_levelCondition.ConditionCompleteEvent -= GameOver;

            Destroy(m_levelCondition);
            m_levelCondition = null;
        }
    }
}
