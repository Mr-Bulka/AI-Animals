using UnityEngine;
using System.Collections;

public class InteractionSystem : MonoBehaviour
{
    private EndocrineSystem endocrine;
    private MemorySystem memory;
    private InternalIndicators indicators;
    private Genetics genetics;
    private MotorSystem motor;
    private ThoughtBubble thoughtBubble;
    private Brain brain;

    private string MyName => GetComponent<InteractableObject>() != null ? GetComponent<InteractableObject>().ObjectName : gameObject.name;

    void Awake()
    {
        endocrine = GetComponent<EndocrineSystem>();
        memory = GetComponent<MemorySystem>();
        indicators = GetComponent<InternalIndicators>();
        genetics = GetComponent<Genetics>();
        motor = GetComponent<MotorSystem>();
        thoughtBubble = GetComponent<ThoughtBubble>();
        brain = GetComponent<Brain>();
    }

    private IEnumerator WiggleEffect(float duration)
    {
        float elapsed = 0f;
        Vector3 origScale = transform.localScale;
        while (elapsed < duration)
        {
            if (indicators.IsDead) break; 
            transform.localScale = origScale * (1f + Mathf.Sin(elapsed * 25f) * 0.1f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = origScale;
    }

    public void HandleInteraction(InteractableObject obj)
    {
        if (brain.isPerformingAction || obj == null) return;
        
        // Если мы в охоте/бою, но укус ещё в перезарядке:
        if ((obj.type == ObjectType.Creature || obj.type == ObjectType.Danger) && brain.IsHostileTowards(obj) && brain.attackCooldownTimer > 0)
        {
            float dist = Vector2.Distance(transform.position, obj.transform.position);
            if (dist > motor.interactionRange + 0.2f && !motor.IsMoving)
            {
                motor.MoveToAndInteract(obj, 2.5f);
            }
            return;
        }

        StartCoroutine(InteractionSequence(obj));
    }

    private IEnumerator InteractionSequence(InteractableObject obj)
    {
        brain.isPerformingAction = true;
        motor.StopMoving();

        float startSatiety = indicators.Satiety;
        float startBoredom = indicators.Boredom;
        float startPain = indicators.Pain;
        float addedSocialBond = 0f;
        bool consumed = false;

        Debug.Log($"[Взаимодействие] Начинаю контакт с {obj.ObjectName}");
        
        // При прямом физическом контакте существо МГНОВЕННО запоминает, где находится этот объект!
        if (memory != null && obj != null)
        {
            memory.RememberLocation(obj.ObjectName, obj.type, obj.transform.position);
        }
        
        // === ОСНОВНАЯ ЛОГИКА ВЗАИМОДЕЙСТВИЯ ===
        if (obj == null) { brain.isPerformingAction = false; yield break; }

        if (obj.type == ObjectType.Toy || obj.funValue > 0)
        {
            if (thoughtBubble != null) thoughtBubble.ShowThought("Уиии!");
            yield return StartCoroutine(WiggleEffect(1f));
            if (obj == null || indicators.IsDead) goto EndSequence;

            Debug.Log($"[Игра] Поиграл с {obj.ObjectName}");
            ExperienceRecord mem = memory.GetExperience(obj.ObjectName);
            float actualFun = obj.funValue / (1f + mem.playSatiety);
            indicators.Boredom -= actualFun;
            indicators.Energy -= 10f;
            endocrine.AddPleasure(10f);
            endocrine.AddEndorphin(5f);
            memory.RecordPlayInteraction(obj.ObjectName);
            
            Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                Vector2 pushDir = (obj.transform.position - transform.position).normalized;
                rb.AddForce(pushDir * 8f, ForceMode2D.Impulse);
            }
        }
        else if (obj.type == ObjectType.Food || obj.nutritionValue > 0)
        {
            float nutVal = obj.nutritionValue > 0 ? obj.nutritionValue : 35f;
            string originalName = obj.ObjectName.StartsWith("Труп ") ? obj.ObjectName.Substring(5) : obj.ObjectName;
            ExperienceRecord mem = memory.GetExperience(originalName);
            float requiredTabooBond = (brain != null && indicators.Satiety <= brain.GetHuntSatietyThreshold()) ? 75f : 50f;
            if (obj.ObjectName.StartsWith("Труп ") && mem.socialBond >= requiredTabooBond)
            {
                if (thoughtBubble != null) thoughtBubble.ShowThought("О нет... Мой друг...");
                Debug.Log($"[Траур] {gameObject.name} отказывается есть {obj.ObjectName}, потому что это друг.");
                endocrine.AddStress(30f);
                indicators.Loneliness += 20f;
                brain.DecideToAvoid(obj, obj.transform.position);
                brain.isPerformingAction = false;
                yield break;
            }

            if (indicators.Satiety < 99f)
            {
                if (thoughtBubble != null) thoughtBubble.ShowThought("Ом-ном-ном...");
                yield return StartCoroutine(WiggleEffect(2f));
                if (obj == null || indicators.IsDead) goto EndSequence;

                SensorySystem mySensors = GetComponent<SensorySystem>();
                if (mySensors != null)
                {
                    foreach (var sensed in mySensors.SensedObjects)
                    {
                        if (sensed.actualObject != null && sensed.actualObject.type == ObjectType.Creature)
                        {
                            InteractionSystem witness = sensed.actualObject.GetComponent<InteractionSystem>();
                            if (witness != null && witness != this)
                            {
                                witness.OnWitnessedCorpseEaten(brain, obj.ObjectName);
                            }
                        }
                    }
                }

                Debug.Log($"[Питание] Съел {obj.ObjectName}");
                indicators.Eat(nutVal);
                endocrine.AddPleasure(10f);
                consumed = true;
            }
            else
            {
                Debug.Log($"[Любопытство] Обнюхал {obj.ObjectName}, но есть не стал (сыт).");
                endocrine.AddPleasure(2f);
            }
        }
        else if (obj.type == ObjectType.FoodSource)
        {
            FruitTree tree = obj.GetComponent<FruitTree>();
            if (tree != null)
            {
                if (genetics != null && genetics.Intelligence > 65f)
                {
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Трясу...");
                    yield return StartCoroutine(WiggleEffect(1.5f));
                    if (obj == null || indicators.IsDead) goto EndSequence;

                    int dropCount = 1;
                    if (genetics.Strength >= 1.4f) dropCount = 4;
                    else if (genetics.Strength >= 1.15f) dropCount = 3;
                    else if (genetics.Strength >= 0.95f) dropCount = 2;

                    int actuallyDropped = 0;
                    for (int i = 0; i < dropCount; i++)
                    {
                        if (tree.DropOneFruit()) actuallyDropped++;
                        else break;
                    }

                    if (actuallyDropped > 1)
                    {
                        if (thoughtBubble != null) thoughtBubble.ShowThought($"Могучий удар! +{actuallyDropped} яблока!");
                        indicators.Energy -= 8f;
                        indicators.Boredom -= 5f;
                    }
                    else if (actuallyDropped == 1)
                    {
                        if (thoughtBubble != null) thoughtBubble.ShowThought("Стряс яблоко!");
                        indicators.Energy -= 5f;
                        indicators.Boredom -= 2f;
                    }
                    else
                    {
                        if (thoughtBubble != null) thoughtBubble.ShowThought("Тут пусто...");
                    }
                }
                else
                {
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Жду чуда...");
                    yield return StartCoroutine(WiggleEffect(1f));
                }
            }
        }
        else if (brain != null && brain.IsHostileTowards(obj))
        {
            if (thoughtBubble != null) thoughtBubble.ShowThought("Кусь!");
            if (obj == null || indicators.IsDead) goto EndSequence;

            // Вычисляем задержку перед следующей атакой на основе характеристик: сильные и смелые атакуют быстрее
            float attackSpeedFactor = (genetics != null) ? (genetics.Strength / Mathf.Max(0.5f, genetics.Fearfulness)) : 1f;
            brain.attackCooldownTimer = Mathf.Clamp(1.8f / attackSpeedFactor, 0.5f, 3.5f);
            
            float damageDealt = 20f * (genetics != null ? genetics.Strength : 1f);
            Debug.Log($"[Агрессия] {gameObject.name} КУСАЕТ {obj.ObjectName} на {damageDealt:F1} урона!");
            
            indicators.Satiety = Mathf.Min(100f, indicators.Satiety + 5f);
            endocrine.Cortisol *= 0.5f;
            endocrine.AddPleasure(10f);
            addedSocialBond = -50f;

            Brain otherBrain = obj.GetComponent<Brain>();
            InternalIndicators otherIndicators = obj.GetComponent<InternalIndicators>();
            
            if (otherIndicators == null)
            {
                otherIndicators = obj.gameObject.AddComponent<InternalIndicators>();
                bool isAlpha = obj.GetComponent<AlphaHunter>() != null || obj.ObjectName.Contains("Альфа") || obj.ObjectName.Contains("Alpha");
                float defaultHp = isAlpha ? 1000f : 150f;
                otherIndicators.MaxHealth = defaultHp;
                otherIndicators.Health = defaultHp;
            }

            otherIndicators.ReceiveDamage(damageDealt);
            
            if (otherBrain != null)
            {
                if (otherBrain.TryGetComponent(out EndocrineSystem otherEndo)) 
                {
                    otherEndo.AddStress(80f);
                    otherEndo.Adrenaline = 100f; // Взрываем адреналин и стресс для максимального страха!
                }
                if (otherBrain.TryGetComponent(out MemorySystem otherMem)) otherMem.RecordExperience(MyName, new ExperienceRecord { deltaPain = damageDealt, socialBond = -50f });
                
                Genetics otherGenetics = otherBrain.GetComponent<Genetics>();
                bool otherIsHostileToUs = otherBrain.IsHostileTowards(GetComponent<InteractableObject>());

                if (otherGenetics != null && otherGenetics.Fearfulness < 1.3f && otherIndicators.Health > 30f)
                {
                    if (otherBrain.GetComponent<ThoughtBubble>() != null) otherBrain.GetComponent<ThoughtBubble>().ShowThought("Получай!");
                    float defenseDamage = 10f * otherGenetics.Strength;
                    indicators.ReceiveDamage(defenseDamage);
                    endocrine.AddStress(30f);
                    memory.RecordExperience(obj.ObjectName, new ExperienceRecord { deltaPain = defenseDamage, socialBond = -50f });
                    
                    if (!otherIsHostileToUs)
                        otherBrain.GetComponent<MotorSystem>().MoveAway(transform.position, 2f);
                }
                else
                {
                    if (otherBrain.GetComponent<ThoughtBubble>() != null) otherBrain.GetComponent<ThoughtBubble>().ShowThought("ААА! Больно!");
                    if (!otherIsHostileToUs)
                        otherBrain.GetComponent<MotorSystem>().MoveAway(transform.position, 3f);
                }
            }
            else
            {
                // Если атакуем Hunter без компонента Brain, получаем немного защитного урона от контакта в смертельной схватке
                if (obj.painValue > 0 && otherIndicators.Health > 0)
                {
                    indicators.ReceiveDamage(obj.painValue * 0.5f);
                    endocrine.AddStress(15f);
                }
            }

            // После укуса продолжаем стремительную погоню к цели, если она ещё жива!
            if (obj != null && obj.gameObject.activeInHierarchy && otherIndicators.Health > 0 && brain.IsHostileTowards(obj))
            {
                motor.MoveToAndInteract(obj, 2.5f);
            }
        }
        else if (obj.painValue > 0)
        {
            indicators.ReceiveDamage(obj.painValue);
            endocrine.AddStress(40f); 
            brain.DecideToAvoid(obj, obj.transform.position);
        }
        else if (obj.type == ObjectType.Creature)
        {
            Brain otherBrain = obj.GetComponent<Brain>();
            ExperienceRecord mem = memory.GetExperience(obj.ObjectName);
            
            if (brain.socialCooldownTimer > 0)
            {
                brain.isPerformingAction = false;
                yield break;
            }
            if (otherBrain != null && otherBrain.GetComponent<InternalIndicators>().IsSleeping)
            {
                if (thoughtBubble != null) thoughtBubble.ShowThought("Тссс, спит...");
                brain.isPerformingAction = false;
                yield break;
            }
            if (otherBrain != null && otherBrain.socialCooldownTimer > 0)
            {
                if (thoughtBubble != null) thoughtBubble.ShowThought("Он не в настроении...");
                brain.isPerformingAction = false;
                yield break;
            }

            if (otherBrain != null)
            {
                if (endocrine.Curiosity > 60f && indicators.Boredom > 40f)
                {
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Догонялки!");
                    yield return StartCoroutine(WiggleEffect(1f));
                    if (obj == null || indicators.IsDead) goto EndSequence;

                    float actualFunSelf = 40f / (1f + mem.playSatiety);
                    float actualFunOther = 40f / (1f + otherBrain.GetComponent<MemorySystem>().GetExperience(MyName).playSatiety);

                    indicators.Boredom = Mathf.Max(0, indicators.Boredom - actualFunSelf);
                    endocrine.AddEndorphin(20f);
                    endocrine.AddOxytocin(20f);
                    addedSocialBond = 35f;
                    
                    motor.MoveToRandomPoint(10f, 2.5f);
                    memory.RecordPlayInteraction(obj.ObjectName);

                    otherBrain.GetComponent<InternalIndicators>().Boredom = Mathf.Max(0, otherBrain.GetComponent<InternalIndicators>().Boredom - actualFunOther);
                    otherBrain.GetComponent<EndocrineSystem>().AddEndorphin(20f);
                    otherBrain.GetComponent<EndocrineSystem>().AddOxytocin(20f);
                    otherBrain.GetComponent<MemorySystem>().RecordExperience(MyName, new ExperienceRecord { deltaBoredom = -actualFunOther, socialBond = 35f });
                    
                    otherBrain.GetComponent<MotorSystem>().MoveToRandomPoint(10f, 2.5f);
                    otherBrain.GetComponent<MemorySystem>().RecordPlayInteraction(MyName);
                }
                else
                {
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Привет!");
                    yield return StartCoroutine(WiggleEffect(1.5f));
                    if (obj == null || indicators.IsDead) goto EndSequence;

                    indicators.Loneliness -= 50f;
                    endocrine.AddOxytocin(30f);
                    endocrine.AddPleasure(10f);
                    addedSocialBond = 25f;
                    
                    string myStory = memory.GetMostSignificantMemoryPhrase();
                    string theirStory = otherBrain.GetComponent<MemorySystem>().GetMostSignificantMemoryPhrase();

                    memory.MergeMemories(otherBrain.GetComponent<MemorySystem>().GetAllMemories());
                    memory.MergeSpatialMemories(otherBrain.GetComponent<MemorySystem>().GetAllSpatialRecords());
                    
                    otherBrain.GetComponent<MemorySystem>().MergeMemories(memory.GetAllMemories());
                    otherBrain.GetComponent<MemorySystem>().MergeSpatialMemories(memory.GetAllSpatialRecords());
                    
                    if (thoughtBubble != null && !string.IsNullOrEmpty(myStory))
                        thoughtBubble.ShowThought(myStory);
                    if (otherBrain.GetComponent<ThoughtBubble>() != null && !string.IsNullOrEmpty(theirStory))
                        otherBrain.GetComponent<ThoughtBubble>().ShowThought(theirStory);
                        
                    otherBrain.GetComponent<MemorySystem>().RecordExperience(MyName, new ExperienceRecord { socialBond = 25f });
                }

                float maxCooldown = (genetics != null) ? (10f / genetics.Sociability) : 10f;
                brain.socialCooldownTimer = maxCooldown;
                otherBrain.socialCooldownTimer = (otherBrain.GetComponent<Genetics>() != null) ? (10f / otherBrain.GetComponent<Genetics>().Sociability) : 10f;
            }
        }

    EndSequence:
        if (obj != null)
        {
            ExperienceRecord exp = new ExperienceRecord();
            exp.deltaSatiety = indicators.Satiety - startSatiety;
            exp.deltaBoredom = indicators.Boredom - startBoredom;
            exp.deltaPain = indicators.Pain - startPain;
            exp.socialBond = addedSocialBond;
            
            bool isUnknown = (exp.deltaSatiety == 0 && exp.deltaBoredom == 0 && exp.deltaPain == 0 && exp.socialBond == 0);
            if (isUnknown && !consumed) exp.deltaBoredom = -1f; 
            
            memory.RecordExperience(obj.ObjectName, exp);
            
            if (consumed)
            {
                memory.ForgetExactObject(obj.ObjectName); 
                obj.gameObject.SetActive(false); // Сразу отключаем, чтобы сенсоры не успели снова его запомнить до уничтожения
                Destroy(obj.gameObject);
            }
        }
        
        brain.isPerformingAction = false;
    }

    public void OnWitnessedCorpseEaten(Brain eater, string corpseName)
    {
        if (indicators.IsDead) return;
        
        string originalName = corpseName.StartsWith("Труп ") ? corpseName.Substring(5) : corpseName;
        ExperienceRecord mem = memory.GetExperience(originalName);
        if (mem.socialBond >= 50f)
        {
            Debug.Log($"[Осквернение] {gameObject.name} увидел, как {eater.gameObject.name} ест его друга {corpseName}!");
            if (thoughtBubble != null) thoughtBubble.ShowThought("Как ты мог?!");
            endocrine.AddStress(80f);
            indicators.Loneliness += 50f;
            
            ExperienceRecord expEater = new ExperienceRecord { socialBond = -100f };
            memory.RecordExperience(eater.gameObject.name, expEater); 
            
            if (genetics == null || genetics.Fearfulness < 1.3f)
            {
                endocrine.AddStress(50f); 
                motor.MoveToAndInteract(eater.GetComponent<InteractableObject>(), 2f);
            }
            else
            {
                endocrine.AddStress(50f);
                motor.MoveAway(eater.transform.position, 3f);
            }
        }
    }
}
