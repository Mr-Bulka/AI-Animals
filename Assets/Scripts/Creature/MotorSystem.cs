using UnityEngine;

public class MotorSystem : MonoBehaviour
{
    public float baseMoveSpeed = 2f;
    [Tooltip("На каком расстоянии существо может взаимодействовать с объектами (мячом, едой)")]
    public float interactionRange = 3f;
    
    private float currentSpeed = 2f;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private InteractableObject currentTarget;
    private InteractionSystem interactionSystem; // Прямая ссылка для гарантированного вызова
    
    public bool IsMoving => isMoving;
    public float CurrentSpeed => currentSpeed;
    public InteractableObject CurrentTarget => currentTarget;
    public Vector2 FacingDirection { get; private set; } = Vector2.left;
    
    private SpriteRenderer spriteRenderer;
    
    public delegate void InteractionHandler(InteractableObject obj);
    public event InteractionHandler OnInteractionComplete;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionSystem = GetComponent<InteractionSystem>();
    }

    /// <summary>
    /// Гарантированный вызов взаимодействия: сначала прямой вызов, потом событие.
    /// </summary>
    private void FireInteraction(InteractableObject obj)
    {
        if (obj == null) return;
        
        if (interactionSystem == null)
            interactionSystem = GetComponentInChildren<InteractionSystem>();

        if (interactionSystem != null)
        {
            interactionSystem.HandleInteraction(obj);
        }
        
        OnInteractionComplete?.Invoke(obj);
    }

    void Update()
    {
        if (isMoving)
        {
            if (currentTarget != null)
            {
                targetPosition = currentTarget.transform.position;
            }

            Vector2 direction = targetPosition - (Vector2)transform.position;
            if (direction != Vector2.zero)
            {
                FacingDirection = direction.normalized;
            }

            if (spriteRenderer != null && direction.x != 0)
            {
                spriteRenderer.flipX = direction.x > 0;
            }

            transform.position = Vector2.MoveTowards(transform.position, targetPosition, currentSpeed * Time.deltaTime);
            
            float stopDistance = (currentTarget != null) ? interactionRange : 0.1f;

            if (Vector2.Distance((Vector2)transform.position, targetPosition) <= stopDistance)
            {
                isMoving = false;
                if (currentTarget != null)
                {
                    InteractableObject arrivedTarget = currentTarget;
                    currentTarget = null;
                    FireInteraction(arrivedTarget);
                }
                else
                {
                    TryAutoInteractNearby();
                }
            }
        }
    }
    
    public void MoveToAndInteract(InteractableObject obj, float speedMultiplier = 1f)
    {
        currentSpeed = baseMoveSpeed * speedMultiplier;
        currentTarget = obj;
        targetPosition = obj.transform.position;
        isMoving = true;
    }
    
    public void MoveAway(Vector2 dangerPos, float speedMultiplier = 2.5f)
    {
        currentSpeed = baseMoveSpeed * Mathf.Max(speedMultiplier, 2.5f); // Гарантированный быстрый бег при панике
        currentTarget = null;
        Vector2 dir = ((Vector2)transform.position - dangerPos).normalized;
        targetPosition = (Vector2)transform.position + dir * 16f; // Отбегаем далеко на 16 юнитов!
        isMoving = true;
    }
    
    public void MoveToRandomPoint(float radius, float speedMultiplier = 0.5f)
    {
        currentSpeed = baseMoveSpeed * speedMultiplier;
        currentTarget = null;
        Vector2 randomOffset = Random.insideUnitCircle * radius;
        targetPosition = (Vector2)transform.position + randomOffset;
        isMoving = true;
    }

    public void MoveToPosition(Vector2 pos, float speedMultiplier = 1f)
    {
        currentSpeed = baseMoveSpeed * speedMultiplier;
        currentTarget = null;
        targetPosition = pos;
        isMoving = true;
    }
    
    public void StopMoving()
    {
        isMoving = false;
        currentTarget = null;
        targetPosition = transform.position;
    }
    
    public void SetSleepVisuals(bool isSleeping)
    {
        if (spriteRenderer != null)
        {
            if (isSleeping)
            {
                spriteRenderer.transform.rotation = Quaternion.Euler(0, 0, -90); // Ложимся на бок
            }
            else
            {
                spriteRenderer.transform.rotation = Quaternion.identity; // Встаем
            }
        }
    }

    // Абсолютно надежный метод через физику: 
    // если мы буквально врезались в цель (или вошли в триггер), засчитываем контакт!
    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckPhysicalContact(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        CheckPhysicalContact(collider.gameObject);
    }

    private void CheckPhysicalContact(GameObject touchedObject)
    {
        InteractableObject obj = touchedObject.GetComponent<InteractableObject>() ?? touchedObject.GetComponentInParent<InteractableObject>() ?? touchedObject.GetComponentInChildren<InteractableObject>();
        if (obj == null || obj.gameObject == this.gameObject) return;

        if (currentTarget == obj || obj.type == ObjectType.Food || obj.type == ObjectType.Toy || (isMoving && (obj.type == ObjectType.FoodSource || obj.type == ObjectType.Creature)))
        {
            // Мгновенный контакт при соприкосновении с едой, игрушкой или целевым объектом:
            isMoving = false;
            InteractableObject targetToInteract = obj;
            currentTarget = null;
            
            FireInteraction(targetToInteract);
        }
    }

    /// <summary>
    /// При прибытии в точку по памяти (без конкретной цели) ищем ближайший интерактивный объект
    /// в радиусе взаимодействия и переключаемся на нормальное преследование.
    /// </summary>
    private void TryAutoInteractNearby()
    {
        // Если мозг уже занят взаимодействием — не пытаемся начинать новое
        Brain brain = GetComponent<Brain>();
        if (brain != null && brain.isPerformingAction) return;

        Collider2D[] nearby = new Collider2D[10];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;
        int count = Physics2D.OverlapCircle(transform.position, interactionRange, filter, nearby);

        InteractableObject bestObj = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (nearby[i] == null) continue;
            InteractableObject obj = nearby[i].GetComponent<InteractableObject>() ?? nearby[i].GetComponentInParent<InteractableObject>();
            if (obj == null || obj.gameObject == this.gameObject) continue;
            
            if (obj.type == ObjectType.Food || obj.type == ObjectType.FoodSource || obj.type == ObjectType.Toy || obj.type == ObjectType.Creature)
            {
                float dist = Vector2.Distance(transform.position, obj.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestObj = obj;
                }
            }
        }

        if (bestObj != null)
        {
            Debug.Log($"[Мотор] Прибыл по памяти, нашел {bestObj.ObjectName} рядом! Переключаюсь на цель.");
            // Переключаемся на нормальное преследование объекта: MotorSystem сам вызовет OnInteractionComplete
            // когда дистанция <= interactionRange (а мы уже рядом, так что это произойдет в следующем кадре)
            MoveToAndInteract(bestObj, 1.5f);
        }
    }
}
