using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class PlayingBoardController : MonoBehaviour
{
    public event System.Action OnBoardFullEvent = delegate { }; // Event khi board đầy
    public event System.Action<Item, Cell> OnItemClickedInPlayingBoard = delegate { }; // Event khi click item trong playing board (Attack Time mode)

    public bool IsBusy { get; private set; }

    private Board m_playingBoard;
    private GameSettings m_gameSettings;
    private Camera m_cam;

    // Attack Time mode flag
    private bool m_isAttackTimeMode = false;

    // Tracking next empty slot
    private int m_nextEmptySlotIndex = 0;

    public void StartGame(GameSettings gameSettings)
    {
        m_gameSettings = gameSettings;
        m_cam = Camera.main;

        // Tạo playing board (board dưới)
        m_playingBoard = new Board(this.transform, gameSettings, 1, 5);
        // Board này khởi tạo RỖNG (không fill items)
    }

    // Set Attack Time mode
    public void SetAttackTimeMode(bool isAttackTime)
    {
        m_isAttackTimeMode = isAttackTime;
    }

    // Update để xử lý click trong Attack Time mode
    private void Update()
    {
        if (!m_isAttackTimeMode) return;
        if (IsBusy) return;

        // Handle click vào item trong playing board để remove
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = m_cam.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);

            if (hit.collider != null)
            {
                Cell clickedCell = hit.collider.GetComponent<Cell>();

                // Check cell có thuộc playing board không và có item không
                if (clickedCell != null && IsPlayingBoardCell(clickedCell) && !clickedCell.IsEmpty)
                {
                    Debug.Log($"🔙 Clicked item in Playing Board: {clickedCell.Item}");
                    OnItemClickedInPlayingBoard?.Invoke(clickedCell.Item, clickedCell);
                }
            }
        }
    }

    // Check cell có thuộc playing board không
    private bool IsPlayingBoardCell(Cell cell)
    {
        return cell.transform.IsChildOf(this.transform);
    }

    // Nhận item từ inventory board (không cần drop position)
    public void ReceiveItemFromInventory(Item item)
    {
        // Tìm slot trống tiếp theo
        Cell targetCell = GetNextEmptySlot();

        if (targetCell != null)
        {
            Debug.Log($"📥 Receiving item to slot. Empty slots remaining: {CountEmptySlots()}");
            PlaceItemOnBoard(item, targetCell);
        }
        else
        {
            Debug.Log("⚠️ Playing board is FULL! No empty slots available.");
            Debug.Log("🔴 Triggering OnBoardFullEvent -> GAME OVER");
            // Trigger event: Board đầy -> Game Over
            OnBoardFullEvent?.Invoke();
        }
    }

    // Đếm số slot trống
    private int CountEmptySlots()
    {
        Cell[] allCells = m_playingBoard.GetAllCells();
        int count = 0;
        foreach (Cell cell in allCells)
        {
            if (cell.IsEmpty) count++;
        }
        return count;
    }

    // Tìm slot trống tiếp theo (từ trái qua phải)
    private Cell GetNextEmptySlot()
    {
        Cell[] allCells = m_playingBoard.GetAllCells();

        for (int i = 0; i < allCells.Length; i++)
        {
            if (allCells[i].IsEmpty)
            {
                return allCells[i];
            }
        }

        return null; // Không còn slot trống
    }

    // Nhận item từ inventory board (legacy - giữ lại để tương thích)
    public void ReceiveItemFromInventory(Item item, Vector3 dropPosition)
    {
        // Gọi method mới (không dùng dropPosition)
        ReceiveItemFromInventory(item);
    }

    private void PlaceItemOnBoard(Item item, Cell targetCell)
    {
        IsBusy = true;

        // Assign item vào cell
        targetCell.Assign(item);

        // Trigger "Move" animation nếu có Animator
        Animator animator = item.View.GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Move");
            Debug.Log($"🚀 Triggered Move animation for {item.View.name}");
        }

        // Animate item bay đến vị trí
        item.View.transform.DOMove(targetCell.transform.position, 0.3f)
            .OnComplete(() =>
            {
                // Trigger "Land" animation khi đến nơi (optional)
                if (animator != null)
                {
                    animator.SetTrigger("Land");
                    Debug.Log($"📍 Triggered Land animation for {item.View.name}");
                }

                CheckAndDespawnMatches();
            });
    }

    private void CheckAndDespawnMatches()
    {
        // Đếm số lượng từng loại item trên board
        Dictionary<int, List<Cell>> itemGroups = new Dictionary<int, List<Cell>>();
        Cell[] allCells = m_playingBoard.GetAllCells();

        foreach (Cell cell in allCells)
        {
            if (cell.IsEmpty) continue;

            // Lấy type của item (dựa vào NormalItem.ItemType)
            if (cell.Item is NormalItem normalItem)
            {
                int itemType = (int)normalItem.ItemType;

                if (!itemGroups.ContainsKey(itemType))
                {
                    itemGroups[itemType] = new List<Cell>();
                }

                itemGroups[itemType].Add(cell);
            }
        }

        // Tìm nhóm có >= 3 items giống nhau
        List<Cell> matchesToDespawn = null;
        foreach (var group in itemGroups)
        {
            if (group.Value.Count >= 3)
            {
                matchesToDespawn = group.Value;
                break; // Chỉ xử lý nhóm đầu tiên tìm thấy
            }
        }

        if (matchesToDespawn != null && matchesToDespawn.Count >= 3)
        {
            DespawnMatches(matchesToDespawn);
        }
        else
        {
            // Không có match nào -> check xem board có đầy không
            if (!HasEmptySlots())
            {
                Debug.Log("⚠️ No matches and board is FULL! Triggering Game Over...");
                OnBoardFullEvent?.Invoke();
            }

            IsBusy = false;
        }
    }

    private void DespawnMatches(List<Cell> matches)
    {
        // Despawn (xóa) tất cả items match
        foreach (var cell in matches)
        {
            cell.ExplodeItem();
            cell.Free();
        }

        // Sau 0.3s → shift items xuống (nếu cần)
        StartCoroutine(ShiftDownAndCheckAgain());
    }

    private IEnumerator ShiftDownAndCheckAgain()
    {
        yield return new WaitForSeconds(0.3f);

        // Shift items xuống (giống match-3 classic)
        m_playingBoard.ShiftDownItems();

        yield return new WaitForSeconds(0.2f);

        // Check lại matches sau khi shift (cascade)
        CheckAndDespawnMatches();

        // Note: IsBusy sẽ được set = false trong CheckAndDespawnMatches() nếu không còn match
    }

    // Kiểm tra board còn chỗ trống không
    public bool HasEmptySlots()
    {
        Cell[] allCells = m_playingBoard.GetAllCells();

        foreach (Cell cell in allCells)
        {
            if (cell.IsEmpty)
            {
                return true;
            }
        }

        return false;
    }
}