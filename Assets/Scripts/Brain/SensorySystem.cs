using System.Collections.Generic;
using UnityEngine;

public struct SensedObject
{
    public InteractableObject actualObject;
    public Vector2 perceivedPosition;
    public bool isIdentified;
    public bool isLocationPrecise;
    public bool hasAggressiveScent;
}

public class SensorySystem : MonoBehaviour
{
    [Header("Настройки восприятия")]
    [Tooltip("Дальность направленного зрения (силуэты)")]
    public float visionRadius = 40f;
    [Tooltip("Дальность четкого зрения (опознавание)")]
    public float recognitionRadius = 28f;
    [Tooltip("Радиус кругового чутья (запахи, слух)")]
    public float smellRadius = 20f;
    [Tooltip("Угол обзора (в градусах)")]
    [Range(10, 360)] public float visionAngle = 120f;
    
    [Tooltip("Насколько сильно 'размазываются' координаты запаха")]
    public float smellBlurDistance = 5f;

    public LayerMask interactableLayer;
    
    // Событие, когда мы замечаем новый объект
    public delegate void ObjectDetectedHandler(SensedObject obj);
    public event ObjectDetectedHandler OnObjectDetected;
    
    private List<SensedObject> sensedObjects = new List<SensedObject>();
    private MemorySystem memory;
    private MotorSystem motor;
    private Genetics genetics;
    private Dictionary<InteractableObject, float> observationTimers = new Dictionary<InteractableObject, float>();
    
    // Публичное свойство для доступа к видимым объектам из мозга
    public List<SensedObject> SensedObjects => sensedObjects;
    
    private Collider2D[] overlapResults = new Collider2D[250];
    private float scanTimer = 0f;
    private const float SCAN_INTERVAL = 0.1f;

    void Awake()
    {
        memory = GetComponent<MemorySystem>();
        motor = GetComponent<MotorSystem>();
        genetics = GetComponent<Genetics>();
    }
    
    void Update()
    {
        scanTimer += Time.deltaTime;
        if (scanTimer < SCAN_INTERVAL) return;
        scanTimer = 0f;

        float maxRange = Mathf.Max(visionRadius, smellRadius * 2f);
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        if (interactableLayer.value != 0)
        {
            filter.useLayerMask = true;
            filter.layerMask = interactableLayer;
        }
        else
        {
            filter.useLayerMask = false;
        }
        int hitCount = Physics2D.OverlapCircle(transform.position, maxRange, filter, overlapResults);
        
        List<SensedObject> currentSensed = new List<SensedObject>();
        List<InteractableObject> currentlyObservedKeys = new List<InteractableObject>();
        Vector2 facingDir = motor != null ? motor.FacingDirection : Vector2.left;
        
        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = overlapResults[i];
            InteractableObject obj = col.GetComponent<InteractableObject>() ?? col.GetComponentInParent<InteractableObject>();
            if (obj != null && obj.gameObject != this.gameObject)
            {
                Vector2 dirToObject = (Vector2)obj.transform.position - (Vector2)transform.position;
                float distance = dirToObject.magnitude;
                
                float effectiveSmellRadius = smellRadius;
                bool hasAggroScent = false;

                if (obj.type == ObjectType.Creature)
                {
                    EndocrineSystem otherEndo = obj.GetComponent<EndocrineSystem>();
                    if (otherEndo != null && (otherEndo.Aggression > 50f || otherEndo.Adrenaline > 50f))
                    {
                        // Запах агрессивного и возбужденного сородича распространяется на дальнее расстояние (вплоть до х2)!
                        float hormoneIntensity = Mathf.Max(otherEndo.Aggression, otherEndo.Adrenaline);
                        effectiveSmellRadius += smellRadius * (hormoneIntensity / 100f);
                        if (distance <= effectiveSmellRadius)
                        {
                            hasAggroScent = true;
                        }
                    }
                }
                else if (obj.type == ObjectType.Danger || obj.ObjectName.Contains("Альфа") || obj.ObjectName.Contains("Охотник") || obj.ObjectName.Contains("Hunter"))
                {
                    // Мощный запах опасности и хищника! Жертва чует погоню даже спиной во время бега на огромной дистанции
                    effectiveSmellRadius = Mathf.Max(effectiveSmellRadius, 16f);
                    hasAggroScent = true;
                }

                bool inSmell = distance <= effectiveSmellRadius;
                bool inVision = distance <= visionRadius && Vector2.Angle(facingDir, dirToObject) <= visionAngle / 2f;
                bool inRecognition = distance <= recognitionRadius && inVision;
                
                if (!inSmell && !inVision) continue; // Объект вне зоны досягаемости любых чувств
                
                SensedObject sensed = new SensedObject();
                sensed.actualObject = obj;
                sensed.hasAggressiveScent = hasAggroScent;
                // Идентифицируем объект, если чувствуем его запах ИЛИ четко видим (а опасность узнаём инстинктивно издали!)
                sensed.isIdentified = inSmell || inRecognition || obj.type == ObjectType.Danger;
                // Точное положение известно, только если объект в поле зрения (даже если это далекий силуэт)
                sensed.isLocationPrecise = inVision || (inSmell && obj.type == ObjectType.Danger);
                
                if (sensed.isLocationPrecise)
                {
                    sensed.perceivedPosition = obj.transform.position;
                }
                else
                {
                    // Имитируем "размытый" запах: детерминированное смещение без обращения к Unity Random (во избежание проблем со сбросом сида и крашей)
                    int hash = obj.gameObject.GetHashCode();
                    float angle = (hash % 360) * Mathf.Deg2Rad;
                    float dist = ((Mathf.Abs(hash / 360) % 100) / 100f) * smellBlurDistance;
                    Vector2 blurOffset = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
                    sensed.perceivedPosition = (Vector2)obj.transform.position + blurOffset;
                }

                currentSensed.Add(sensed);
                
                // Проверяем, видели ли мы его в прошлом кадре
                bool wasAlreadySensed = false;
                foreach (var oldSensed in sensedObjects)
                {
                    if (oldSensed.actualObject == obj)
                    {
                        wasAlreadySensed = true;
                        break;
                    }
                }
                
                if (!wasAlreadySensed)
                {
                    OnObjectDetected?.Invoke(sensed);
                }
                
                if (memory != null && sensed.isIdentified)
                {
                    bool knowsObject = memory.HasExperience(obj.ObjectName);
                    if (knowsObject)
                    {
                        // Знакомый объект освежается в памяти сразу со своей реальной категорией
                        memory.RememberLocation(obj.ObjectName, obj.type, sensed.perceivedPosition);
                    }
                    else if (obj.type == ObjectType.Danger || obj.type == ObjectType.Creature || obj.ObjectName.Contains("Альфа") || obj.ObjectName.Contains("Охотник"))
                    {
                        // Опасность и сородичи распознаются инстинктивно, без раздумий!
                        memory.RememberLocation(obj.ObjectName, obj.type, sensed.perceivedPosition);
                    }
                    else
                    {
                        // Незнакомый мирный объект требует времени на наблюдение и запоминается как Unknown
                        if (!observationTimers.ContainsKey(obj)) observationTimers[obj] = 0f;
                        observationTimers[obj] += SCAN_INTERVAL;
                        currentlyObservedKeys.Add(obj);
                        
                        float iq = (genetics != null) ? genetics.Intelligence : 50f;
                        float timeToMemorize = Mathf.Max(1f, 11f - (iq / 10f)); 

                        if (observationTimers[obj] >= timeToMemorize)
                        {
                            memory.RememberLocation(obj.ObjectName, ObjectType.Unknown, sensed.perceivedPosition);
                        }
                    }
                }
            }
        }
        
        List<InteractableObject> keysToDrop = new List<InteractableObject>();
        foreach (var key in observationTimers.Keys)
        {
            if (!currentlyObservedKeys.Contains(key)) keysToDrop.Add(key);
        }
        foreach (var key in keysToDrop) observationTimers.Remove(key);

        sensedObjects = currentSensed;
        
        // Очистка памяти от объектов, которых больше нет в прямой зоне видимости (ближнее четкое зрение)
        if (memory != null)
        {
            memory.CleanUpMissingObjects(sensedObjects, transform.position, recognitionRadius, facingDir, visionAngle);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 1. Отрисовка радиуса запаха (Круговое чутье)
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, smellRadius);

        // 2. Отрисовка конуса зрения
        Vector2 forward = Vector2.left; // Изначально смотрим влево
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.flipX) forward = Vector2.right;

        // Если игра запущена, берем точное направление из мотора
        MotorSystem motor = GetComponent<MotorSystem>();
        if (Application.isPlaying && motor != null && motor.FacingDirection != Vector2.zero)
        {
            forward = motor.FacingDirection;
        }

        Vector3 leftRay = Quaternion.Euler(0, 0, visionAngle / 2f) * forward;
        Vector3 rightRay = Quaternion.Euler(0, 0, -visionAngle / 2f) * forward;
        
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(transform.position, transform.position + leftRay * visionRadius);
        Gizmos.DrawLine(transform.position, transform.position + rightRay * visionRadius);
        
        // Рисуем дугу конуса зрения (максимальная дальность)
        int segments = 20;
        float angleStep = visionAngle / segments;
        Vector3 currentPos = transform.position + rightRay * visionRadius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 nextDir = Quaternion.Euler(0, 0, -visionAngle / 2f + angleStep * i) * forward;
            Vector3 nextPos = transform.position + nextDir * visionRadius;
            Gizmos.DrawLine(currentPos, nextPos);
            currentPos = nextPos;
        }

        // 3. Отрисовка радиуса четкого распознавания (зеленая дуга)
        Gizmos.color = new Color(0f, 1f, 0f, 0.6f);
        Vector3 currentRecPos = transform.position + rightRay * recognitionRadius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 nextDir = Quaternion.Euler(0, 0, -visionAngle / 2f + angleStep * i) * forward;
            Vector3 nextPos = transform.position + nextDir * recognitionRadius;
            Gizmos.DrawLine(currentRecPos, nextPos);
            currentRecPos = nextPos;
        }
    }
#endif
}
