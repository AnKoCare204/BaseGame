using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class R3Input
{
    [SerializeField] private InputActionReference PressAction;
    [SerializeField] private InputActionReference PositionAction;
    [SerializeField] private float ClickThreshold = 0.2f;
    [SerializeField] private float ClickMoveThreshold = 10f;
    private Vector2 pressPosition;
    private float pressTime;

    private InputAction Press => PressAction != null ? PressAction.action : null;
    private InputAction Position => PositionAction != null ? PositionAction.action : null;

    public bool IsConfigured => Press != null && Position != null;

    public Vector2 PointerPosition
    {
        get
        {
            InputAction position = Position;
            if (position != null) return position.ReadValue<Vector2>();
            Pointer pointer = Pointer.current;
            return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
        }
    }

    public void Enable()
    {
        if (Press == null || Position == null)
        {
            Debug.LogError("[R3Input] Chưa gán PressAction / PositionAction trên InputManager.");
            return;
        }

        if (!Press.enabled) Press.Enable();
        if (!Position.enabled) Position.Enable();
    }

    public void Disable()
    {
        pressTime = 0f;
    }

    public bool WasPressedThisFrame(Unit _) => Press != null && Press.WasPressedThisFrame();

    public bool IsPressed(Unit _) => Press != null && Press.IsPressed();

    public bool WasReleasedThisFrame(Unit _) => Press != null && Press.WasReleasedThisFrame();
    
    public bool WasClicked(Unit _)
    {
        InputAction press = Press;
        if (press == null) return false;
        if (press.WasPressedThisFrame())
        {
            pressTime = Time.time;
            pressPosition = PointerPosition;
        }

        if (press.WasReleasedThisFrame())
        {
            float duration = Time.time - pressTime;
            float distance = (PointerPosition - pressPosition).sqrMagnitude;
            return duration <= ClickThreshold && distance <= ClickMoveThreshold * ClickMoveThreshold;
        }

        return false;
    }
}
