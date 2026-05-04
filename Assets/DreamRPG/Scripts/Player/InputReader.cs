using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // 🟢 THÊM SỰ KIỆN CHO SKILL 1 (Phím V) VÀ SKILL 2 (Phím F)
    public event Action Skill1Event;
    public event Action Skill2Event;
    public event Action Skill3Event;

    private PlayerControls controls;

    private void Awake() {
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
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
        if (controls != null)
        {
            controls.Player.Disable();
        }
    }

    public void OnMove(InputAction.CallbackContext context) => MovementValue = context.ReadValue<Vector2>();
    public void OnLook(InputAction.CallbackContext context) => LookValue = context.ReadValue<Vector2>();

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsJumping = true;
            JumpEvent?.Invoke();
        }
        else if (context.canceled) IsJumping = false;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        if (context.performed) IsSprinting = true;
        else if (context.canceled) IsSprinting = false;
    }

    public void OnInteract(InputAction.CallbackContext context) { }
    public void OnCrouch(InputAction.CallbackContext context) { }
    public void OnPrevious(InputAction.CallbackContext context) { }
    public void OnNext(InputAction.CallbackContext context) { } 
    
    public void OnRoll(InputAction.CallbackContext context)
    {
        if (context.performed) 
        {
            IsRolling = true;
            RollEvent?.Invoke();
        }
        else if (context.canceled)
        {
            IsRolling = false;
        }
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            IsHoldingAim = true;
            AimEvent?.Invoke(); 
        }
        else if (context.canceled)
        {
            IsHoldingAim = false;
        }
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            IsHoldingAttack = true;
            AttackEvent?.Invoke();
        }
        else if (context.canceled)
        {
            IsHoldingAttack = false;
        }
    }

    // 🟢 ĐÓN TÍN HIỆU TỪ BẢNG INPUT ACTIONS (KHI BẤM NÚT V)
    public void OnSkill1(InputAction.CallbackContext context)
    {
        if (context.performed) Skill1Event?.Invoke();
    }

    public void OnSkill2(InputAction.CallbackContext context)
    {
        if (context.started) 
        { 
            IsHoldingSkill2 = true; 
            Skill2Event?.Invoke(); // Gọi Event để bắt đầu chạy State
        }
        else if (context.canceled) 
        { 
            IsHoldingSkill2 = false; // Khi sếp buông tay, biến này sẽ thành false
        }
    }
    public void OnSkill3(InputAction.CallbackContext context)
    {
        // Phản đòn thì cần độ nhạy cao nhất, dùng .performed là chuẩn bài
        if (context.performed) Skill3Event?.Invoke(); 
    }

    public void OnBlock(InputAction.CallbackContext context)
    {
        if (context.performed) IsHoldingBlock = true;
        else if (context.canceled) IsHoldingBlock = false;
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
}