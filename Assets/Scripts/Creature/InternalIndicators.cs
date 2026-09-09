using UnityEngine;

public class InternalIndicators : MonoBehaviour
{
    public float MaxHealth = 100f;
    public float Health = 100f;
    [Range(0, 100)] public float Satiety = 100f; // Сытость
    [Range(0, 100)] public float Energy = 100f; // Энергия/сон
    [Tooltip("От 0 до 100. Растет, если существо не спит, но стоит на месте без дела.")]
    [Range(0, 100)] public float Boredom = 0f;

    [Tooltip("От 0 до 100. Растет, если существо долго ни с кем не общалось.")]
    [Range(0, 100)] public float Loneliness = 0f;
    
    // Временные индикаторы
    public float Pain = 0f;
    
    [Header("Состояния")]
    [Tooltip("Флаг состояния сна")]
    public bool IsSleeping = false;
    
    [Header("Жизнь и Смерть")]
    public bool IsDead = false;
    
    private MotorSystem motor;
    private EndocrineSystem endocrine;

    void Start()
    {
        motor = GetComponent<MotorSystem>();
        endocrine = GetComponent<EndocrineSystem>();

        // Зависимость здоровья от физической силы (если у существа есть гены и это не босс-охотник)
        if (TryGetComponent<Genetics>(out Genetics gen) && GetComponent<AlphaHunter>() == null)
        {
            MaxHealth = Mathf.Round(100f * gen.Strength);
            Health = MaxHealth;
        }

        // Хаос при рождении: никто не рождается с идеальными 100% показателями
        Satiety = Random.Range(50f, 100f);
        Energy = Random.Range(70f, 100f);
        Boredom = Random.Range(0f, 30f);
        Loneliness = Random.Range(0f, 20f);
    }

    void Update()
    {
        if (IsDead) return;

        if (IsSleeping)
        {
            // Во сне метаболизм замедлен в 5 раз
            Satiety -= (Time.deltaTime * 0.5f) / 5f;
            Energy += Time.deltaTime * 5f; // Восстанавливаем энергию
            Boredom -= Time.deltaTime * 5f; // Мозг отдыхает, скука уходит
            
            // Естественная регенерация (если не голодает)
            if (Satiety > 30f && Health < MaxHealth)
            {
                Health += Time.deltaTime * (0.5f * (MaxHealth / 100f)); // Восстанавливаем здоровье пропорционально макс ХП
            }
        }
        else
        {
            // Бодрствование: нормальный расход сытости (0.5f)
            Satiety -= Time.deltaTime * 0.5f;
            
            // Расход энергии зависит от скорости бега, однако всплеск Адреналина блокирует усталость!
            float speedMod = (motor != null && motor.IsMoving && motor.baseMoveSpeed > 0) 
                ? (motor.CurrentSpeed / motor.baseMoveSpeed) 
                : 0f;
            float adrenalineFactor = (endocrine != null) ? Mathf.Clamp01(1f - (endocrine.Adrenaline / 40f)) : 1f;
            float energyDrain = 0.3f + (speedMod * 0.6f * adrenalineFactor);
            
            Energy -= Time.deltaTime * energyDrain;
            Loneliness += Time.deltaTime * 1.5f; // Одиночество растет во время бодрствования
        }
        
        Satiety = Mathf.Clamp(Satiety, 0, 100);
        Energy = Mathf.Clamp(Energy, 0, 100);
        Boredom = Mathf.Clamp(Boredom, 0, 100);
        Loneliness = Mathf.Clamp(Loneliness, 0, 100);
        Health = Mathf.Clamp(Health, 0, MaxHealth);
        
        if (Satiety <= 0f)
        {
            ReceiveDamage(Time.deltaTime * 2f); // Умирает от голода
        }
        
        // Боль со временем проходит
        if (Pain > 0)
        {
            Pain -= Time.deltaTime * 10f;
            Pain = Mathf.Max(Pain, 0);
        }

        if (Health <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        IsDead = true;
        Health = 0;
        Debug.Log($"[СМЕРТЬ] {gameObject.name} умер.");
        
        // Отключаем мозг и системы
        if (TryGetComponent(out Brain brain)) brain.enabled = false;
        if (TryGetComponent(out MotorSystem motor)) 
        {
            motor.StopMoving();
            motor.enabled = false;
        }
        if (TryGetComponent(out SensorySystem sensory)) sensory.enabled = false;
        if (TryGetComponent(out EndocrineSystem endocrine)) endocrine.enabled = false;
        
        // Меняем тип объекта на Еду (мясо)
        if (TryGetComponent(out InteractableObject obj))
        {
            obj.type = ObjectType.Food;
            obj.nutritionValue = 50f;
            obj.ObjectName = "Труп " + obj.ObjectName;
        }

        // Визуал смерти
        if (TryGetComponent(out SpriteRenderer sr))
        {
            sr.transform.rotation = Quaternion.Euler(0, 0, 180); // Вверх ногами
            sr.color = new Color(0.6f, 0.6f, 0.6f); // Бледный цвет
        }
    }
    
    public void ReceiveDamage(float amount)
    {
        Health -= amount;
        Pain += amount * 2f;
        if (Health <= 0 && !IsDead)
        {
            Die();
        }
    }
    
    public void Eat(float nutrition)
    {
        Satiety += nutrition;
    }
}
