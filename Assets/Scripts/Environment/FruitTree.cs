using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InteractableObject))]
public class FruitTree : MonoBehaviour
{
    [Header("Настройки спавна")]
    [Tooltip("Префаб еды (яблоко), который будет падать с дерева")]
    public GameObject foodPrefab;
    
    [Tooltip("Пустые объекты-дочерние точки, где будут появляться фрукты на кроне")]
    public Transform[] spawnPoints;
    
    [Tooltip("Время (в секундах) полного созревания урожая")]
    public float growTime = 15f;

    [Header("Сбор урожая (God Mode)")]
    [Tooltip("Сколько раз нужно кликнуть по дереву, чтобы стрясти фрукты")]
    public int clicksToHarvest = 3;
    
    [Tooltip("Визуальная тряска дерева")]
    public float shakeIntensity = 0.1f;

    private float currentGrowTimer = 0f;
    private int currentClicks = 0;
    
    // Список фруктов, которые сейчас висят на ветках
    private List<GameObject> growingFruits = new List<GameObject>();
    private bool isFullyGrown = false;
    
    public bool HasFruits => growingFruits.Count > 0;
    
    private Vector3 originalPosition;

    void Start()
    {
        originalPosition = transform.position;
        // Начинаем рост с нуля
        currentGrowTimer = 0f;
    }

    void Update()
    {
        if (!isFullyGrown)
        {
            currentGrowTimer += Time.deltaTime;
            if (currentGrowTimer >= growTime)
            {
                GrowFruits();
            }
        }
    }

    private void GrowFruits()
    {
        isFullyGrown = true;
        currentClicks = 0; // Сбрасываем клики

        if (foodPrefab == null || spawnPoints.Length == 0) return;

        foreach (Transform point in spawnPoints)
        {
            // Создаем фрукт на ветке
            GameObject fruit = Instantiate(foodPrefab, point.position, Quaternion.identity, transform);
            
            // "Усыпляем" физику и логику, пока фрукт на дереве
            Rigidbody2D rb = fruit.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;

            Collider2D coll = fruit.GetComponent<Collider2D>();
            if (coll != null) coll.enabled = false;

            InteractableObject interactable = fruit.GetComponent<InteractableObject>();
            if (interactable != null) interactable.enabled = false;

            growingFruits.Add(fruit);
        }
        
        Debug.Log($"[Дерево] Урожай на {gameObject.name} созрел!");
    }

    // Этот метод будет вызываться из SelectionManager, когда игрок (Бог) кликает по дереву
    public void OnGodClick()
    {
        if (!isFullyGrown)
        {
            Debug.Log($"[Дерево] {gameObject.name} еще не созрело. Ждите.");
            return;
        }

        currentClicks++;
        
        // Визуальная тряска дерева
        StartCoroutine(ShakeEffect());

        if (currentClicks >= clicksToHarvest)
        {
            HarvestFruits();
        }
    }

    private void HarvestFruits()
    {
        Debug.Log($"[Божественное вмешательство] Вы стрясли фрукты с {gameObject.name}!");

        foreach (GameObject fruit in growingFruits)
        {
            if (fruit == null) continue;

            // Отвязываем от дерева
            fruit.transform.SetParent(null);

            // "Пробуждаем" физику, чтобы яблоко отскочило
            Rigidbody2D rb = fruit.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                
                // Так как у нас вид сверху (Top-Down), гравитация должна быть равна 0,
                // иначе яблоки "провалятся" вниз за пределы экрана!
                rb.gravityScale = 0f;
                rb.linearDamping = 3f; // Трение, чтобы яблоко не катилось вечно
                
                // Добавляем легкий случайный разброс в стороны
                Vector2 dropForce = new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f));
                rb.AddForce(dropForce, ForceMode2D.Impulse);
            }

            Collider2D coll = fruit.GetComponent<Collider2D>();
            if (coll != null) coll.enabled = true;

            InteractableObject interactable = fruit.GetComponent<InteractableObject>();
            if (interactable != null) interactable.enabled = true; // Теперь существа его видят!
        }

        growingFruits.Clear();
        isFullyGrown = false;
        currentGrowTimer = 0f; // Запускаем цикл роста заново
    }

    // Вызывается умным существом, которое бьет по дереву
    public bool DropOneFruit()
    {
        if (growingFruits.Count > 0)
        {
            GameObject fruit = growingFruits[growingFruits.Count - 1];
            growingFruits.RemoveAt(growingFruits.Count - 1);
            
            if (fruit != null)
            {
                fruit.transform.SetParent(null);
                Rigidbody2D rb = fruit.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.gravityScale = 0f;
                    rb.linearDamping = 3f;
                    Vector2 dropForce = new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f));
                    rb.AddForce(dropForce, ForceMode2D.Impulse);
                }
                Collider2D coll = fruit.GetComponent<Collider2D>();
                if (coll != null) coll.enabled = true;

                InteractableObject interactable = fruit.GetComponent<InteractableObject>();
                if (interactable != null) interactable.enabled = true;
                
                // Визуально потрясти дерево немного
                StartCoroutine(ShakeEffect());
                
                // Если фруктов больше нет, сбрасываем статус созревания
                if (growingFruits.Count == 0)
                {
                    isFullyGrown = false;
                    currentGrowTimer = 0f;
                }
                return true;
            }
        }
        return false;
    }

    private IEnumerator ShakeEffect()
    {
        float elapsed = 0f;
        float shakeDuration = 0.2f;

        while (elapsed < shakeDuration)
        {
            Vector3 randomOffset = (Vector3)Random.insideUnitCircle * shakeIntensity;
            transform.position = originalPosition + randomOffset;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = originalPosition;
    }
}
