using UnityEngine;
using UnityEngine.InputSystem;

public class SelectionManager : MonoBehaviour
{
    [Tooltip("Ссылка на UI существа, куда будут передаваться данные выделенного объекта")]
    public CreatureUI creatureUI;

    [Tooltip("Объект-индикатор (например, спрайт круга), который будет появляться под выделенным существом")]
    public GameObject selectionRing;

    [Tooltip("Смещение кольца относительно центра существа (например, опустить вниз к ногам: Y = -0.5)")]
    public Vector3 selectionOffset = new Vector3(0f, -0.5f, 0f);

    private Transform currentTarget;

    void Start()
    {
        if (selectionRing != null)
        {
            selectionRing.SetActive(false);
            selectionRing.transform.SetParent(null); // Убеждаемся, что кольцо ни к чему не привязано
        }
    }

    void Update()
    {
        // Постоянно следим за выбранной целью, не привязываясь к ней иерархически
        if (currentTarget != null && selectionRing != null && selectionRing.activeSelf)
        {
            selectionRing.transform.position = currentTarget.position + selectionOffset;
        }

        if (Mouse.current == null) return;

        // По клику ЛКМ пытаемся выделить существо
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Защита: Если клик пришелся на элемент интерфейса (UI), мы его игнорируем, 
            // чтобы панель не закрывалась при попытке скроллинга.
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector3 mousePosScreen = Mouse.current.position.ReadValue();
            Vector2 mousePosWorld = Camera.main.ScreenToWorldPoint(mousePosScreen);
            
            // Используем OverlapPoint, он надежнее для кликов по 2D-объектам, чем Raycast
            Collider2D hitCollider = Physics2D.OverlapPoint(mousePosWorld);

            if (hitCollider != null)
            {
                // Сначала проверяем, не кликнули ли мы по дереву
                FruitTree tree = hitCollider.GetComponent<FruitTree>();
                if (tree != null)
                {
                    tree.OnGodClick();
                    return; // Это действие Бога, не пытаемся выделить дерево как существо
                }

                Debug.Log($"[SelectionManager] Клик попал в коллайдер: {hitCollider.gameObject.name}");
                InteractableObject obj = hitCollider.GetComponent<InteractableObject>();
                
                if (obj != null)
                {
                    Debug.Log($"[SelectionManager] У объекта есть InteractableObject с типом: {obj.type}");
                    if (obj.type == ObjectType.Creature)
                    {
                        if (creatureUI != null)
                        {
                            creatureUI.BindToCreature(hitCollider.gameObject);
                        }
                        else
                        {
                            Debug.LogError("[SelectionManager] ОШИБКА: Не назначено поле Creature UI в инспекторе!");
                        }
                        
                        if (selectionRing != null)
                        {
                            currentTarget = hitCollider.transform;
                            selectionRing.SetActive(true);
                        }
                    }
                }
                else
                {
                    Debug.Log($"[SelectionManager] На объекте {hitCollider.gameObject.name} нет скрипта InteractableObject.");
                }
            }
            else
            {
                // Клик по пустому пространству - снимаем выделение
                currentTarget = null;
                if (selectionRing != null) selectionRing.SetActive(false);
                if (creatureUI != null) creatureUI.ClearBinding();
            }
        }
    }
}
