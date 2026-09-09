using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Для проверки UI

public class CameraController : MonoBehaviour
{
    [Header("Перемещение")]
    [Tooltip("Скорость перемещения камеры (WASD или стрелочки)")]
    public float panSpeed = 15f;
    [Tooltip("Сглаживание перетаскивания мышью")]
    public float dragDamping = 10f;

    [Header("Зум")]
    [Tooltip("Чувствительность колесика мыши")]
    public float zoomSensitivity = 3f;
    [Tooltip("Сглаживание зума")]
    public float zoomDamping = 8f;
    [Tooltip("Минимальный размер камеры (максимальное приближение)")]
    public float minZoom = 0.1f;
    [Tooltip("Максимальный размер камеры (максимальное отдаление)")]
    public float maxZoom = 1000f;

    private Camera cam;
    private float targetZoom;

    // Переменные для перетаскивания (Drag)
    private Vector3 dragOriginWorld;
    private bool isDragging = false;
    private Vector3 targetPosition;

    void Start()
    {
        // Защита от старых значений в инспекторе
        if (maxZoom < 1000f) maxZoom = 1000f;

        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        
        if (cam != null)
        {
            targetZoom = cam.orthographicSize;
            targetPosition = transform.position;
        }
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || cam == null) return;

        HandleZoom();
        HandleMovement();
    }

    private void HandleZoom()
    {
        // Защита: если курсор мыши находится над UI (например, над панелью существа), 
        // мы игнорируем скролл, чтобы не отдалять камеру при прокрутке списков.
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        float rawScroll = Mouse.current.scroll.y.ReadValue();
        if (rawScroll != 0f)
        {
            // Берем только направление скролла (1 или -1), чтобы мышки с разными драйверами работали одинаково
            float scrollDir = Mathf.Sign(rawScroll);
            
            // Базовый шаг зума (чем выше sensitivity, тем меньше множитель, тем резче зум)
            float zoomFactor = Mathf.Clamp(1f - (zoomSensitivity * 0.05f), 0.1f, 0.99f);
            
            if (scrollDir > 0) 
                targetZoom *= zoomFactor; // Приближаем
            else 
                targetZoom /= zoomFactor; // Отдаляем
                
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        // Плавное приближение к целевому зуму (Lerp)
        if (Mathf.Abs(cam.orthographicSize - targetZoom) > 0.01f)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomDamping);
        }
    }

    private void HandleMovement()
    {
        // 1. WASD управление (работает всегда)
        float x = 0f;
        float y = 0f;

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) x += 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) x -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) y -= 1f;
        
        if (x != 0 || y != 0)
        {
            Vector3 moveDir = new Vector3(x, y, 0).normalized;
            targetPosition += moveDir * panSpeed * Time.deltaTime;
        }

        // 2. Управление правой кнопкой мыши (Drag)
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            isDragging = true;
        }
        
        if (Mouse.current.rightButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (isDragging)
        {
            // Берем смещение мыши с прошлого кадра (в пикселях экрана)
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            
            // Вычисляем, сколько мировых единиц в 1 пикселе экрана
            float unitsPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            
            // Инвертируем, так как если мы тянем мышь вправо, камера должна двигаться влево
            Vector3 worldDelta = new Vector3(-mouseDelta.x * unitsPerPixel, -mouseDelta.y * unitsPerPixel, 0);
            
            targetPosition += worldDelta;
        }

        // Плавное перемещение к целевой позиции (Инерция)
        if (Vector3.Distance(transform.position, targetPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * dragDamping);
        }
    }
}
