using UnityEngine;

public class DualBoardGameManager : MonoBehaviour
{
    public bool IsBusy { get; private set; }
    public bool IsAttackTimeMode { get; private set; } // Flag để biết mode nào

    private InventoryBoardController m_inventoryBoard;
    private PlayingBoardController m_playingBoard;
    private GameSettings m_gameSettings;
    private GameManager m_gameManager;

    // StartGame được gọi từ GameManager (giống BoardController)
    public void StartGame(GameManager gameManager, GameSettings gameSettings)
    {
        m_gameManager = gameManager;
        m_gameSettings = gameSettings;

        SetupBoards();
    }

    // Setup cho Attack Time mode
    public void SetAttackTimeMode(bool isAttackTime)
    {
        IsAttackTimeMode = isAttackTime;
    }

    private void SetupBoards()
    {
        // Tạo Inventory Board (board trên)
        GameObject inventoryObj = new GameObject("InventoryBoard");
        inventoryObj.transform.SetParent(this.transform);
        inventoryObj.transform.position = new Vector3(-1.5f, 0f, 0); // Đặt ở trên
        m_inventoryBoard = inventoryObj.AddComponent<InventoryBoardController>();
        m_inventoryBoard.StartGame(m_gameManager, m_gameSettings);

        // Subscribe events từ inventory
        m_inventoryBoard.OnItemSelectedFromInventory += OnItemSelectedFromInventory;
        m_inventoryBoard.OnItemConfirmedFromInventory += OnItemConfirmedFromInventory;
        m_inventoryBoard.OnItemDraggedFromInventory += OnItemDraggedToPlayingBoard;
        m_inventoryBoard.OnInventoryEmptyEvent += OnInventoryEmpty; // Subscribe WIN event

        // Tạo Playing Board (board dưới) - board này sẽ RỖNG
        GameObject playingObj = new GameObject("PlayingBoard");
        playingObj.transform.SetParent(this.transform);
        playingObj.transform.position = new Vector3(6, 0, 0); // Đặt ở dưới
        m_playingBoard = playingObj.AddComponent<PlayingBoardController>();
        m_playingBoard.StartGame(m_gameSettings);
        m_playingBoard.SetAttackTimeMode(IsAttackTimeMode); // Set mode

        // Subscribe event khi board đầy (chỉ cho non-Attack-Time mode)
        if (!IsAttackTimeMode)
        {
            m_playingBoard.OnBoardFullEvent += OnPlayingBoardFull;
        }

        // Subscribe event click item trong playing board để remove về inventory (Attack Time mode)
        if (IsAttackTimeMode)
        {
            m_playingBoard.OnItemClickedInPlayingBoard += OnPlayingBoardItemClicked;
        }

        // Pass playing board reference to inventory (để check IsBusy)
        m_inventoryBoard.SetPlayingBoardReference(m_playingBoard);

        IsBusy = false;
    }

    // Khi item được select (highlight)
    private void OnItemSelectedFromInventory(Item item, Cell sourceCell)
    {
        Debug.Log($"🔵 Item selected: {item}");
        // Có thể thêm visual feedback ở đây (ví dụ: highlight slot trống tiếp theo trong playing board)
    }

    // Khi item được confirm chuyển sang playing board
    private void OnItemConfirmedFromInventory(Item item, Cell sourceCell)
    {
        Debug.Log($"✅ Item confirmed! Transferring to playing board...");

        // Check playing board có chỗ trống không
        if (m_playingBoard.HasEmptySlots())
        {
            m_playingBoard.ReceiveItemFromInventory(item);
            Debug.Log($"✅ Item transferred successfully!");
        }
        else
        {
            Debug.Log("⚠️ Playing board is full! Cannot transfer item.");
            // Có thể return item về inventory hoặc hiển thị thông báo
        }
    }

    // Khi Inventory Board rỗng -> WIN!
    private void OnInventoryEmpty()
    {
        Debug.Log("🎉 VICTORY! All items cleared from inventory!");
        Debug.Log("✅ Triggering WIN condition -> Game WIN state");

        // Gọi GameWin thay vì GameOver
        if (m_gameManager != null)
        {
            m_gameManager.GameWin();
        }
    }    // Khi Playing Board đầy -> Game Over (chỉ cho non-Attack-Time mode)
    private void OnPlayingBoardFull()
    {
        Debug.Log("🔴 GAME OVER! Playing board is full!");

        // Gọi GameOver từ GameManager
        if (m_gameManager != null)
        {
            m_gameManager.GameOver();
        }
    }

    // Khi click vào item trong Playing Board (Attack Time mode) -> Return về Inventory
    private void OnPlayingBoardItemClicked(Item item, Cell sourceCell)
    {
        Debug.Log($"🔙 Returning item {item} from Playing Board to Inventory");

        // Return item về inventory
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.ReceiveItemFromPlayingBoard(item, sourceCell);
        }
    }

    private void OnItemDraggedToPlayingBoard(Item item, Vector3 dropPosition)
    {
        // Check playing board có chỗ trống không
        if (m_playingBoard.HasEmptySlots())
        {
            m_playingBoard.ReceiveItemFromInventory(item, dropPosition);
        }
        else
        {
            Debug.Log("Playing board is full!");
        }
    }

    public void Update()
    {
        if (m_inventoryBoard != null) m_inventoryBoard.Update();
        // PlayingBoard không cần update vì chỉ xử lý khi nhận item
    }

    // Public method để trigger Auto Win
    public void TriggerAutoWin()
    {
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.StartAutoWin();
        }
    }

    // Public method để trigger Auto Lose
    public void TriggerAutoLose()
    {
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.StartAutoLose();
        }
    }

    public void Clear()
    {
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.Clear();
            m_inventoryBoard = null;
        }

        if (m_playingBoard != null)
        {
            // PlayingBoard không có Clear() method, chỉ set null
            m_playingBoard = null;
        }
    }

    private void OnDestroy()
    {
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.OnItemSelectedFromInventory -= OnItemSelectedFromInventory;
            m_inventoryBoard.OnItemConfirmedFromInventory -= OnItemConfirmedFromInventory;
            m_inventoryBoard.OnItemDraggedFromInventory -= OnItemDraggedToPlayingBoard;
            m_inventoryBoard.OnInventoryEmptyEvent -= OnInventoryEmpty; // Unsubscribe WIN event
        }

        if (m_playingBoard != null)
        {
            if (!IsAttackTimeMode)
            {
                m_playingBoard.OnBoardFullEvent -= OnPlayingBoardFull;
            }

            if (IsAttackTimeMode)
            {
                m_playingBoard.OnItemClickedInPlayingBoard -= OnPlayingBoardItemClicked;
            }
        }
    }
}