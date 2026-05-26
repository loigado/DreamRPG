using System;
using UnityEngine;
using UnityEngine.InputSystem;

// 🟢 THÊM LỆNH BLOCK VÀO BỘ ĐỆM
public enum BufferedCommand { None, Attack, Roll, Block }

public class InputReader : MonoBehaviour, PlayerControls.IPlayerActions
{
    public Vector2 MovementValue { get; private set; }
    public Vector2 LookValue { get; private set; }
    
    public bool IsSprinting { get; private set; }
    public bool IsJumping { get; private set; }
    
    public bool IsHoldingAim { get; private set; }
    public bool IsHoldingAttack { get; private set; }
    public bool IsRolling { get; private set; }
    public bool IsHoldingBlock { get; private set; }

    public bool IsHoldingSkill2 { get; private set; }

    public event Action JumpEvent; 
    public event Action RollEvent;
    public event Action AttackEvent;
    public event Action AimEvent; 
    public event Action<int> SwitchWeaponEvent;

    public event Action Skill1Event;
    public event Action Skill2Event;
    public event Action Skill3Event;

    // 🟢 AAA BUFFERING: Các biến quản lý bộ nhớ đệm
    public BufferedCommand BufferedInput { get; private set; } = BufferedCommand.None;
    private float bufferTimer = 0f;
    private const float BUFFER_TIME = 0.3f; // Nhớ phím trong 0.3 giây

    private PlayerControls controls;

    private void Awake() {
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
    }

    // 🟢 AAA BUFFERING: Đếm ngược thời gian lưu phím
    private void Update()
    {
        if (bufferTimer > 0)
        {
            bufferTimer -= Time.deltaTime;
            if (bufferTimer <= 0) BufferedInput = BufferedCommand.None; 
        }
    }

    // Hàm gọi khi State đã sử dụng xong lệnh
    public void ConsumeBuffer()
    {
        BufferedInput = BufferedCommand.None;
        bufferTimer = 0f;
    }

    private void OnEnable()
    {
        if (controls == null)
        {
            controls = new PlayerControls(); 
            controls.Player.SetCallbacks(this);
        }
        controls.Player.Enable();
    }

    private void OnDisable()
    {
        controls.Player.Disable();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MovementValue = context.ReadValue<Vector2>();
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        LookValue = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsJumping = true;
            JumpEvent?.Invoke();
        }
        else if (context.canceled) 
        {
            IsJumping = false;
        }
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed) IsSprinting = true;
        else if (context.canceled) IsSprinting = false;
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        if (context.performed) IsHoldingAim = true;
        else if (context.canceled) IsHoldingAim = false;
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsHoldingAttack = true;
            BufferedInput = BufferedCommand.Attack; // Nạp lệnh vào đệm
            bufferTimer = BUFFER_TIME;              // Kích hoạt đồng hồ
            AttackEvent?.Invoke();
        }
        else if (context.canceled) IsHoldingAttack = false;
    }

    public void OnRoll(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            BufferedInput = BufferedCommand.Roll;  // Nạp lệnh vào đệm
            bufferTimer = BUFFER_TIME;             // Kích hoạt đồng hồ
            RollEvent?.Invoke();
        }
    }

    public void OnSkill1(InputAction.CallbackContext context)
    {
        if (context.performed) Skill1Event?.Invoke(); 
    }

    public void OnSkill2(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsHoldingSkill2 = true;
            Skill2Event?.Invoke(); 
        }
        else if (context.canceled) 
        { 
            IsHoldingSkill2 = false; 
        }
    }

    public void OnSkill3(InputAction.CallbackContext context)
    {
        if (context.performed) Skill3Event?.Invoke(); 
    }

    public void OnBlock(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsHoldingBlock = true;
            
            // 🟢 Nạp lệnh Chuột Phải vào bộ đệm trong 0.3s
            BufferedInput = BufferedCommand.Block; 
            bufferTimer = BUFFER_TIME;             
        }
        else if (context.canceled) 
        {
            IsHoldingBlock = false;
        }
    }

    public void OnSwitchWeapon(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            string keyName = context.control.name; 
            if (keyName == "1" || keyName == "digit1") SwitchWeaponEvent?.Invoke(0); 
            if (keyName == "2" || keyName == "digit2") SwitchWeaponEvent?.Invoke(1); 
            if (keyName == "3" || keyName == "digit3") SwitchWeaponEvent?.Invoke(2); 
        }
    }

    public void OnWeapon1(InputAction.CallbackContext context)
    {
        if (context.performed) SwitchWeaponEvent?.Invoke(0);
    }

    public void OnWeapon2(InputAction.CallbackContext context)
    {
        if (context.performed) SwitchWeaponEvent?.Invoke(1);
    }

    public void OnWeapon3(InputAction.CallbackContext context)
    {
        if (context.performed) SwitchWeaponEvent?.Invoke(2);
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Sau này chúng ta sẽ dùng hàm này để mở Rương, nói chuyện NPC, nhặt Mảnh vỡ...
            // Ví dụ: InteractEvent?.Invoke();
        }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Logic ngồi xổm (nếu có)
        }
    }

    public void OnPrevious(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Logic lùi (thường dùng cho UI hoặc chuyển tab)
        }
    }

    public void OnNext(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Logic tiến (thường dùng cho UI hoặc chuyển tab)
        }
    }
}