using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class InventoryBoardController : MonoBehaviour
{
    // Event: Khi user click item trong inventory
    public event Action<Item, Cell> OnItemSelectedFromInventory = delegate { };
    public event Action<Item, Vector3> OnItemDraggedFromInventory = delegate { };
    public event Action<Item, Cell> OnItemConfirmedFromInventory = delegate { }; // Event mới: khi confirm chuyển item
    public event Action OnInventoryEmptyEvent = delegate { }; // Event mới: khi inventory rỗng -> WIN

    private Board m_inventoryBoard;
    private GameSettings m_gameSettings;
    private GameManager m_gameManager;
    private Camera m_cam;

    // Tracking selected item
    private Cell m_selectedCell;
    private Item m_selectedItem;
    private bool m_hasSelection;

    private bool m_isActive = true;

    // Auto Win variables
    private bool m_isAutoPlaying = false;
    private Coroutine m_autoPlayCoroutine;
    private PlayingBoardController m_playingBoardRef; // Reference to playing board để check IsBusy

    // Auto Lose variables
    private bool m_isAutoLosing = false;
    private Coroutine m_autoLoseCoroutine;

    // Set reference to playing board (called by DualBoardGameManager)
    public void SetPlayingBoardReference(PlayingBoardController playingBoard)
    {
        m_playingBoardRef = playingBoard;
    }

    // Drag & Drop variables
    private bool m_isDragging;
    private Item m_draggedItem;
    private Cell m_sourceCell;

    // Thay Setup() bằng StartGame() giống BoardController
    public void StartGame(GameManager gameManager, GameSettings gameSettings)
    {
        m_gameManager = gameManager;
        m_gameSettings = gameSettings;
        m_cam = Camera.main;

        // Subscribe vào state changes
        m_gameManager.StateChangedAction += OnGameStateChange;

        // Tạo inventory board với size nhỏ hơn
        m_inventoryBoard = new Board(this.transform, gameSettings, 9, 9);

        // Fill inventory với items (custom fill để đảm bảo mỗi loại chia hết cho 3)
        FillInventoryBalanced();
    }

    // Fill inventory với items balanced (mỗi loại chia hết cho 3)
    private void FillInventoryBalanced()
    {
        Cell[] allCells = m_inventoryBoard.GetAllCells();
        int totalCells = allCells.Length; // 9x9 = 81

        // Có 7 loại items (TYPE_ONE -> TYPE_SEVEN)
        int[] itemTypes = new int[] { 0, 1, 2, 3, 4, 5, 6 }; // 7 loại

        // Tính số lượng mỗi loại: 81 / 7 = 11 dư 4
        // Để chia hết cho 3: mỗi loại 12 items (12 * 7 = 84 > 81)
        // Hoặc: 5 loại có 12 items, 2 loại có 10.5 → không được
        // Tốt nhất: 9 items mỗi loại x 9 = 81 items, nhưng chỉ dùng 9/7 = chỉ dùng một số loại

        // Giải pháp: 81 cells, 7 loại
        // 81 / 3 = 27 nhóm, 27 / 7 = 3 dư 6
        // → 6 loại có 12 items (4 nhóm), 1 loại có 9 items (3 nhóm)
        // 12*6 + 9*1 = 72 + 9 = 81 ✓

        List<NormalItem.eNormalType> itemList = new List<NormalItem.eNormalType>();

        // 6 loại đầu: mỗi loại 12 items (chia hết cho 3)
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < 12; j++)
            {
                itemList.Add((NormalItem.eNormalType)i);
            }
        }

        // Loại cuối: 9 items (chia hết cho 3)
        for (int j = 0; j < 9; j++)
        {
            itemList.Add((NormalItem.eNormalType)6);
        }

        // Shuffle list để random vị trí
        ShuffleList(itemList);

        // Assign items vào cells
        for (int i = 0; i < allCells.Length; i++)
        {
            NormalItem item = new NormalItem();
            item.SetType(itemList[i]);
            item.SetView();
            item.SetViewRoot(this.transform);

            allCells[i].Assign(item);
            allCells[i].ApplyItemPosition(false);
        }
    }

    // Shuffle list
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    private void OnGameStateChange(GameManager.eStateGame state)
    {
        switch (state)
        {
            case GameManager.eStateGame.GAME_STARTED:
                m_isActive = true;
                break;
            case GameManager.eStateGame.PAUSE:
                m_isActive = false;
                break;
            case GameManager.eStateGame.GAME_OVER:
                m_isActive = false;
                ClearSelection();
                break;
        }
    }

    public void Update()
    {
        if (!m_isActive) return; // Chỉ xử lý khi game đang chạy

        // Không xử lý input khi auto playing hoặc auto losing
        if (!m_isAutoPlaying && !m_isAutoLosing)
        {
            HandleInventoryInput();
        }
        // HandleDragFromInventory(); // Tạm thời disable drag, dùng click thay thế
    }

    private void HandleInventoryInput()
    {
        // Chỉ xử lý khi click chuột
        if (Input.GetMouseButtonDown(0))
        {
            // Raycast để phát hiện click vào cell nào
            Vector2 mousePos = m_cam.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null)
            {
                Cell clickedCell = hit.collider.GetComponent<Cell>();

                // Kiểm tra cell có thuộc inventory board không
                if (clickedCell != null && IsInventoryCell(clickedCell))
                {
                    OnInventoryItemClicked(clickedCell);
                }
            }
        }
    }

    private bool IsInventoryCell(Cell cell)
    {
        // Kiểm tra cell có thuộc inventory board không
        // (Dựa vào position hoặc parent transform)
        return cell.transform.IsChildOf(this.transform);
    }

    private void OnInventoryItemClicked(Cell cell)
    {
        // Kiểm tra cell có item không
        if (cell.Item == null)
        {
            return;
        }

        // Nếu đã có selection -> click lại thì confirm chuyển item
        if (m_hasSelection && m_selectedCell == cell)
        {
            ConfirmTransferItem();
            return;
        }

        // Clear selection cũ nếu có
        if (m_hasSelection)
        {
            ClearSelection();
        }

        // Lưu selection mới
        m_selectedCell = cell;
        m_selectedItem = cell.Item;
        m_hasSelection = true;

        // Visual feedback: highlight item được chọn
        HighlightSelectedItem(cell);

        // Trigger event cho DualBoardManager xử lý
        OnItemSelectedFromInventory?.Invoke(m_selectedItem, m_selectedCell);
    }

    // Confirm chuyển item sang playing board
    private void ConfirmTransferItem()
    {
        if (m_selectedCell != null && m_selectedItem != null)
        {
            // Trigger event để DualBoardManager xử lý
            OnItemConfirmedFromInventory?.Invoke(m_selectedItem, m_selectedCell);

            // Xóa item khỏi inventory
            m_selectedCell.Free();

            // Clear selection
            ClearSelection();

            // Check xem inventory có rỗng không (WIN condition)
            CheckInventoryEmpty();
        }
    }

    // Kiểm tra inventory có rỗng không
    private void CheckInventoryEmpty()
    {
        if (m_inventoryBoard == null) return;

        Cell[] allCells = m_inventoryBoard.GetAllCells();

        foreach (Cell cell in allCells)
        {
            if (!cell.IsEmpty)
            {
                // Còn item -> chưa win
                return;
            }
        }

        // Tất cả cells đều empty -> WIN!
        OnInventoryEmptyEvent?.Invoke();
    }

    // Đếm số items còn lại trong inventory
    private int CountItemsInInventory()
    {
        if (m_inventoryBoard == null) return 0;

        Cell[] allCells = m_inventoryBoard.GetAllCells();
        int count = 0;

        foreach (Cell cell in allCells)
        {
            if (!cell.IsEmpty) count++;
        }

        return count;
    }

    // Auto Win: Tự động chơi đến khi thắng
    public void StartAutoWin()
    {
        if (m_isAutoPlaying) return; // Đang auto rồi thì không làm gì
        if (m_isAutoLosing) StopAutoLose(); // Dừng auto lose nếu đang chạy

        m_isAutoPlaying = true;
        m_autoPlayCoroutine = StartCoroutine(AutoPlayCoroutine());
    }

    // Stop Auto Win
    public void StopAutoWin()
    {
        if (m_autoPlayCoroutine != null)
        {
            StopCoroutine(m_autoPlayCoroutine);
            m_autoPlayCoroutine = null;
        }
        m_isAutoPlaying = false;
    }

    // Auto Lose: Tự động chơi để thua (chọn ngẫu nhiên items)
    public void StartAutoLose()
    {
        if (m_isAutoLosing) return; // Đang auto lose rồi
        if (m_isAutoPlaying) StopAutoWin(); // Dừng auto win nếu đang chạy

        m_isAutoLosing = true;
        m_autoLoseCoroutine = StartCoroutine(AutoLoseCoroutine());
    }

    // Stop Auto Lose
    public void StopAutoLose()
    {
        if (m_autoLoseCoroutine != null)
        {
            StopCoroutine(m_autoLoseCoroutine);
            m_autoLoseCoroutine = null;
        }
        m_isAutoLosing = false;
    }

    // Coroutine tự động chơi
    private IEnumerator AutoPlayCoroutine()
    {
        while (m_isAutoPlaying && CountItemsInInventory() > 0)
        {
            // Đợi playing board xử lý xong trước khi transfer item tiếp
            if (m_playingBoardRef != null)
            {
                while (m_playingBoardRef.IsBusy)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }

            // Tìm 3 items cùng loại để transfer
            List<Cell> matchingCells = FindThreeMatchingItems();

            if (matchingCells != null && matchingCells.Count == 3)
            {
                // Transfer 3 items liên tiếp
                for (int i = 0; i < 3; i++)
                {
                    Cell cellToTransfer = matchingCells[i];
                    if (cellToTransfer != null && cellToTransfer.Item != null)
                    {
                        // Transfer item
                        Item itemToTransfer = cellToTransfer.Item;
                        OnItemConfirmedFromInventory?.Invoke(itemToTransfer, cellToTransfer);
                        cellToTransfer.Free();

                        // Đợi 0.5s trước khi transfer item tiếp theo
                        yield return new WaitForSeconds(0.5f);

                        // Đợi playing board xử lý
                        if (m_playingBoardRef != null)
                        {
                            while (m_playingBoardRef.IsBusy)
                            {
                                yield return new WaitForSeconds(0.1f);
                            }
                        }
                    }
                }

                // Check inventory empty sau khi transfer xong 3 items
                CheckInventoryEmpty();

                // Kiểm tra inventory còn items không
                if (CountItemsInInventory() == 0)
                {
                    break;
                }
            }
            else
            {
                // Không còn 3 items cùng loại nào
                break;
            }
        }

        m_isAutoPlaying = false;
    }

    // Coroutine tự động chơi để thua (chọn random items)
    private IEnumerator AutoLoseCoroutine()
    {
        while (m_isAutoLosing && CountItemsInInventory() > 0)
        {
            // Đợi playing board xử lý xong trước khi transfer item tiếp
            if (m_playingBoardRef != null)
            {
                while (m_playingBoardRef.IsBusy)
                {
                    yield return new WaitForSeconds(0.1f);
                }

                // Check nếu playing board đã đầy -> dừng (đã thua)
                if (!m_playingBoardRef.HasEmptySlots())
                {
                    break;
                }
            }

            // Lấy 1 item ngẫu nhiên từ inventory
            Cell randomCell = FindRandomItem();

            if (randomCell != null && randomCell.Item != null)
            {
                // Transfer item
                Item itemToTransfer = randomCell.Item;
                OnItemConfirmedFromInventory?.Invoke(itemToTransfer, randomCell);
                randomCell.Free();

                // Đợi 0.5s trước khi transfer item tiếp theo
                yield return new WaitForSeconds(0.5f);

                // Đợi playing board xử lý
                if (m_playingBoardRef != null)
                {
                    while (m_playingBoardRef.IsBusy)
                    {
                        yield return new WaitForSeconds(0.1f);
                    }
                }

                // Check inventory empty (nếu may mắn win)
                CheckInventoryEmpty();

                // Kiểm tra inventory còn items không
                if (CountItemsInInventory() == 0)
                {
                    break;
                }
            }
            else
            {
                // Không còn item nào
                break;
            }
        }

        m_isAutoLosing = false;
    }

    // Tìm 1 item ngẫu nhiên trong inventory
    private Cell FindRandomItem()
    {
        if (m_inventoryBoard == null) return null;

        Cell[] allCells = m_inventoryBoard.GetAllCells();

        // Lấy danh sách tất cả cells có item
        List<Cell> nonEmptyCells = new List<Cell>();
        foreach (Cell cell in allCells)
        {
            if (!cell.IsEmpty && cell.Item is NormalItem)
            {
                nonEmptyCells.Add(cell);
            }
        }

        if (nonEmptyCells.Count == 0)
        {
            return null;
        }

        // Lấy 1 item ngẫu nhiên
        int randomIndex = UnityEngine.Random.Range(0, nonEmptyCells.Count);
        return nonEmptyCells[randomIndex];
    }

    // Tìm 3 items cùng loại để transfer (lấy ngẫu nhiên 1 item, tìm 2 item cùng loại)
    private List<Cell> FindThreeMatchingItems()
    {
        if (m_inventoryBoard == null) return null;

        Cell[] allCells = m_inventoryBoard.GetAllCells();

        // Lấy danh sách tất cả cells có item
        List<Cell> nonEmptyCells = new List<Cell>();
        foreach (Cell cell in allCells)
        {
            if (!cell.IsEmpty && cell.Item is NormalItem)
            {
                nonEmptyCells.Add(cell);
            }
        }

        if (nonEmptyCells.Count == 0)
        {
            return null;
        }

        // Lấy 1 item ngẫu nhiên
        int randomIndex = UnityEngine.Random.Range(0, nonEmptyCells.Count);
        Cell randomCell = nonEmptyCells[randomIndex];
        NormalItem randomItem = randomCell.Item as NormalItem;

        if (randomItem == null)
        {
            return null;
        }

        int targetType = (int)randomItem.ItemType;

        // Tìm tất cả items cùng loại
        List<Cell> matchingCells = new List<Cell>();
        foreach (Cell cell in allCells)
        {
            if (!cell.IsEmpty && cell.Item is NormalItem normalItem)
            {
                if ((int)normalItem.ItemType == targetType)
                {
                    matchingCells.Add(cell);

                    // Chỉ cần 3 items
                    if (matchingCells.Count >= 3)
                    {
                        break;
                    }
                }
            }
        }

        if (matchingCells.Count >= 3)
        {
            return matchingCells.GetRange(0, 3); // Trả về 3 items đầu tiên
        }
        else
        {
            // Thử lại với item type khác
            // Tìm loại item nào có >= 3 items
            Dictionary<int, List<Cell>> itemGroups = new Dictionary<int, List<Cell>>();
            foreach (Cell cell in allCells)
            {
                if (!cell.IsEmpty && cell.Item is NormalItem normalItem)
                {
                    int itemType = (int)normalItem.ItemType;
                    if (!itemGroups.ContainsKey(itemType))
                    {
                        itemGroups[itemType] = new List<Cell>();
                    }
                    itemGroups[itemType].Add(cell);
                }
            }

            // Tìm group đầu tiên có >= 3 items
            foreach (var group in itemGroups)
            {
                if (group.Value.Count >= 3)
                {
                    return group.Value.GetRange(0, 3);
                }
            }

            return null;
        }
    }

    private void HighlightSelectedItem(Cell cell)
    {
        if (cell.Item != null && cell.Item.View != null)
        {
            // Scale up item
            cell.Item.View.transform.localScale = Vector3.one * 1.2f;
        }
    }

    public void ClearSelection()
    {
        if (m_selectedCell != null && m_selectedCell.Item != null && m_selectedCell.Item.View != null)
        {
            // Reset scale
            m_selectedCell.Item.View.transform.localScale = Vector3.one;
        }

        m_selectedCell = null;
        m_selectedItem = null;
        m_hasSelection = false;
    }

    // Nhận item từ Playing Board (Attack Time mode - return item về inventory)
    public void ReceiveItemFromPlayingBoard(Item item, Cell playingBoardCell)
    {
        // Tìm slot trống đầu tiên trong inventory
        Cell emptyCell = FindEmptySlotInInventory();

        if (emptyCell != null)
        {
            // Assign item vào inventory cell (trước khi Free playing board cell)
            emptyCell.Assign(item);

            // Animate item bay về inventory
            if (item.View != null)
            {
                item.View.transform.DOMove(emptyCell.transform.position, 0.3f)
                    .SetEase(Ease.OutQuad);
            }

            // Xóa item khỏi playing board cell (sau khi assign)
            playingBoardCell.Free();
        }
    }

    // Tìm slot trống trong inventory
    private Cell FindEmptySlotInInventory()
    {
        if (m_inventoryBoard == null) return null;

        Cell[] allCells = m_inventoryBoard.GetAllCells();

        foreach (Cell cell in allCells)
        {
            if (cell.IsEmpty)
            {
                return cell;
            }
        }

        return null; // Không còn slot trống
    }

    public void Clear()
    {
        if (m_inventoryBoard != null)
        {
            m_inventoryBoard.Clear();
            m_inventoryBoard = null; // Set null sau khi clear
        }

        // Unsubscribe events
        if (m_gameManager != null)
        {
            m_gameManager.StateChangedAction -= OnGameStateChange;
            m_gameManager = null;
        }
    }

    private void OnDestroy()
    {
        Clear();
    }

    private void HandleDragFromInventory()
    {
        // Click vào inventory item
        if (Input.GetMouseButtonDown(0))
        {
            var hit = Physics2D.Raycast(m_cam.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null)
            {
                m_sourceCell = hit.collider.GetComponent<Cell>();
                if (m_sourceCell != null && m_sourceCell.Item != null)
                {
                    m_isDragging = true;
                    m_draggedItem = m_sourceCell.Item;

                    // Tạo clone để drag (giữ nguyên item gốc)
                    // m_draggedItem.View.SetAlpha(0.5f); // Làm mờ item đang drag
                }
            }
        }

        // Đang drag
        if (Input.GetMouseButton(0) && m_isDragging)
        {
            Vector3 mousePos = m_cam.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            m_draggedItem.View.transform.position = mousePos;
        }

        // Thả item
        if (Input.GetMouseButtonUp(0) && m_isDragging)
        {
            Vector3 dropPosition = m_cam.ScreenToWorldPoint(Input.mousePosition);

            // Trigger event cho PlayingBoard xử lý
            OnItemDraggedFromInventory(m_draggedItem, dropPosition);

            ResetDrag();
        }
    }

    private void ResetDrag()
    {
        m_isDragging = false;
        m_draggedItem = null;
        m_sourceCell = null;
    }

}