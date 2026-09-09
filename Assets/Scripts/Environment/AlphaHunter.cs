using UnityEngine;
using System.Collections;

[RequireComponent(typeof(InteractableObject))]
public class AlphaHunter : MonoBehaviour
{
    [Header("Характеристики Альфа-Охотника")]
    public float maxHealth = 1000f;
    public float attackDamage = 35f;
    public float attackCooldown = 1.5f;
    
    [Header("Скорость (Огромный, тяжелый и медлительный)")]
    public float patrolSpeed = 1.2f;
    public float chaseSpeed = 2.5f;
    public float detectionRange = 18f;
    public float attackRange = 2.5f;
    
    [Header("Охрана территории (Патруль вокруг дерева)")]
    [Tooltip("Дерево или центр, вокруг которого Альфа наматывает круги. Если не указано, автоматически найдёт ближайшее дерево.")]
    public Transform patrolCenter;
    [Tooltip("Радиус кругового патрулирования вокруг дерева")]
    public float patrolRadius = 5.5f;
    [Tooltip("Максимальное расстояние, на которое Альфа готов отойти от дерева при погоне")]
    public float maxChaseDistance = 18f;

    [Header("Визуал")]
    public bool scaleUpAutomatically = true;
    public Color alphaTint = new Color(0.95f, 0.35f, 0.35f, 1f); // Угрожающий багровый оттенок
    [Tooltip("Инвертировать отзеркаливание спрайта, если охотник движется спиной вперёд")]
    public bool invertSpriteFlip = true;

    private InternalIndicators indicators;
    private InteractableObject interactable;
    private SpriteRenderer sr;
    
    private Transform currentPrey;
    private Vector2 patrolTarget;
    private float scanTimer = 0f;
    private float attackTimer = 0f;
    private float patrolTimer = 0f;
    
    private Vector2 initialHomePos;
    private float currentPatrolAngle = 0f;
    private Vector2 GuardCenter => patrolCenter != null ? (Vector2)patrolCenter.position : initialHomePos;

    void Awake()
    {
        interactable = GetComponent<InteractableObject>();
        if (interactable != null)
        {
            interactable.type = ObjectType.Danger;
            interactable.ObjectName = "Альфа_Охотник_" + Random.Range(10, 100);
            interactable.painValue = attackDamage;
        }
    }

    void Start()
    {
        interactable = GetComponent<InteractableObject>();
        if (interactable != null)
        {
            interactable.type = ObjectType.Danger;
            if (string.IsNullOrEmpty(interactable.ObjectName) || !interactable.ObjectName.Contains("Альфа"))
            {
                interactable.ObjectName = "Альфа_Охотник_" + Random.Range(10, 100);
            }
            interactable.painValue = attackDamage;
        }

        indicators = GetComponent<InternalIndicators>();
        if (indicators == null)
        {
            indicators = gameObject.AddComponent<InternalIndicators>();
        }
        indicators.MaxHealth = maxHealth;
        indicators.Health = maxHealth;

        sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = alphaTint;
        }

        if (scaleUpAutomatically && transform.localScale.x < 1.5f)
        {
            transform.localScale = new Vector3(1.8f, 1.8f, 1f); // Делаем его значительно крупнее обычных охотников!
        }

        initialHomePos = transform.position;
        if (patrolCenter == null)
        {
            FruitTree[] trees = Object.FindObjectsByType<FruitTree>(FindObjectsInactive.Exclude);
            float closestDist = float.MaxValue;
            foreach (var tree in trees)
            {
                float dist = Vector2.Distance(transform.position, tree.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    patrolCenter = tree.transform;
                }
            }
        }

        if (patrolCenter != null)
        {
            Debug.Log($"[АЛЬФА-ОХОТНИК] {name} берёт под охрану территорию вокруг {patrolCenter.name}");
        }

        PickNewPatrolTarget();
    }

    void Update()
    {
        if (indicators != null && indicators.IsDead)
        {
            HandleDeathState();
            return;
        }

        if (attackTimer > 0) attackTimer -= Time.deltaTime;
        
        scanTimer -= Time.deltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.3f;
            ScanForPrey();
        }

        if (currentPrey != null && currentPrey.gameObject.activeInHierarchy)
        {
            InternalIndicators preyInd = currentPrey.GetComponent<InternalIndicators>();
            float distToPrey = Vector2.Distance(transform.position, currentPrey.position);
            float distFromGuardCenter = Vector2.Distance(transform.position, GuardCenter);

            // Альфа теряет интерес при разрыве дистанции с жертвой ИЛИ если его уманили слишком далеко от охраняемого дерева!
            if (preyInd != null && preyInd.IsDead || distToPrey > 13f || distFromGuardCenter > maxChaseDistance)
            {
                if (distToPrey > 13f)
                {
                    Debug.Log($"[АЛЬФА-ОХОТНИК] {name} теряет интерес к {currentPrey.name} из-за разрыва дистанции ({distToPrey:F1} м). Возврат к патрулю.");
                }
                else if (distFromGuardCenter > maxChaseDistance)
                {
                    Debug.Log($"[АЛЬФА-ОХОТНИК] {name} отказывается преследовать {currentPrey.name}: нельзя бросать охраняемое дерево слишком надолго!");
                }
                currentPrey = null;
                PickNewPatrolTarget();
            }
            else
            {
                ChaseAndAttack();
                return;
            }
        }

        Patrol();
    }

    private void ScanForPrey()
    {
        Collider2D[] hitColliders = new Collider2D[15];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.useLayerMask = false;
        
        int count = Physics2D.OverlapCircle(transform.position, detectionRange, filter, hitColliders);
        
        float closestDist = float.MaxValue;
        Transform bestTarget = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitColliders[i];
            if (col == null || col.gameObject == gameObject) continue;

            Brain brain = col.GetComponent<Brain>() ?? col.GetComponentInParent<Brain>();
            if (brain == null) continue; // Охотимся только на живых существ с мозгами

            InternalIndicators otherInd = brain.GetComponent<InternalIndicators>();
            if (otherInd == null || otherInd.IsDead || otherInd.Health <= 0) continue;

            float dist = Vector2.Distance(transform.position, brain.transform.position);
            if (dist < closestDist && dist <= detectionRange)
            {
                closestDist = dist;
                bestTarget = brain.transform;
            }
        }

        currentPrey = bestTarget;
    }

    private void ChaseAndAttack()
    {
        float dist = Vector2.Distance(transform.position, currentPrey.position);
        
        // Поворачиваемся к жертве лицом
        if (sr != null)
        {
            float dirX = currentPrey.position.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.05f) sr.flipX = invertSpriteFlip ? (dirX < 0) : (dirX > 0);
        }

        if (dist <= attackRange)
        {
            // Наносим сокрушительный удар Альфа-Охотника!
            if (attackTimer <= 0f)
            {
                AttackPrey();
            }
        }
        else
        {
            // Стремительно бежим за жертвой
            transform.position = Vector2.MoveTowards(transform.position, currentPrey.position, chaseSpeed * Time.deltaTime);
        }
    }

    private void AttackPrey()
    {
        attackTimer = attackCooldown;

        InternalIndicators preyInd = currentPrey.GetComponent<InternalIndicators>();
        Brain preyBrain = currentPrey.GetComponent<Brain>();
        
        if (preyInd != null && !preyInd.IsDead)
        {
            Debug.Log($"[АЛЬФА-ОХОТНИК] {name} наносит сокрушительный удар по {currentPrey.name} на {attackDamage} урона!");
            preyInd.ReceiveDamage(attackDamage);

            if (preyBrain != null)
            {
                if (preyBrain.TryGetComponent(out EndocrineSystem endo))
                {
                    endo.AddStress(90f);
                    endo.Adrenaline = 100f; // Вызывает первобытный ужас и желание немедленно бежать!
                }

                if (preyBrain.TryGetComponent(out ThoughtBubble bubble))
                {
                    bubble.ShowThought("АЛЬФА! СПАСАЙТЕСЬ!");
                }

                if (preyBrain.TryGetComponent(out MemorySystem mem))
                {
                    string objName = interactable != null ? interactable.ObjectName : name;
                    ObjectType objType = interactable != null ? interactable.type : ObjectType.Danger;
                    mem.RecordExperience(objName, new ExperienceRecord { deltaPain = attackDamage, socialBond = -100f });
                    mem.RememberLocation(objName, objType, transform.position); // Жертва навсегда запомнит место, где на неё напали!
                }

                // Заставляем жертву отлетать и удирать на пределе сил
                if (preyBrain.TryGetComponent(out MotorSystem motor))
                {
                    motor.MoveAway(transform.position, 3.5f);
                }
            }
        }
    }

    private void Patrol()
    {
        patrolTimer -= Time.deltaTime;
        float distToTarget = Vector2.Distance(transform.position, patrolTarget);

        if (patrolTimer <= 0f || distToTarget < 0.5f)
        {
            PickNewPatrolTarget();
            return;
        }

        if (sr != null)
        {
            float dirX = patrolTarget.x - transform.position.x;
            if (Mathf.Abs(dirX) > 0.05f) sr.flipX = invertSpriteFlip ? (dirX < 0) : (dirX > 0);
        }

        transform.position = Vector2.MoveTowards(transform.position, patrolTarget, patrolSpeed * Time.deltaTime);
    }

    private void PickNewPatrolTarget()
    {
        patrolTimer = Random.Range(3f, 6f);
        
        // Наматываем круги вокруг дерева (смещаясь по дуге на 45-90 градусов каждый шаг)
        currentPatrolAngle += Random.Range(45f, 90f);
        if (currentPatrolAngle >= 360f) currentPatrolAngle -= 360f;
        
        float rad = currentPatrolAngle * Mathf.Deg2Rad;
        // Небольшие колебания радиусов (+-15%), чтобы обход выглядел грозно и реалистично
        float currentRadius = Random.Range(patrolRadius * 0.85f, patrolRadius * 1.15f);
        Vector2 circleOffset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
        
        patrolTarget = GuardCenter + circleOffset;
    }

    private void HandleDeathState()
    {
        if (interactable != null && interactable.type != ObjectType.Food)
        {
            interactable.type = ObjectType.Food;
            interactable.nutritionValue = 1000f; // Огромный запас мяса от Альфы!
            interactable.ObjectName = "Труп Альфа-Охотника";
            Debug.Log($"[АЛЬФА-ОХОТНИК] {name} БЫЛ ПОВЕРЖЕН! Теперь это огромная гора еды!");
        }

        if (sr != null)
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 180);
            sr.color = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        }
        
        enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        Vector2 center = patrolCenter != null ? (Vector2)patrolCenter.position : (Application.isPlaying ? initialHomePos : (Vector2)transform.position);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(center, patrolRadius);
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.3f);
        Gizmos.DrawWireSphere(center, maxChaseDistance);
    }
}
