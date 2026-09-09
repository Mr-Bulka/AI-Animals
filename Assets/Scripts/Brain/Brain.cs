using UnityEngine;
using System.Collections;
public class Brain : MonoBehaviour
{
    private SensorySystem sensors;
    private EndocrineSystem endocrine;
    private MemorySystem memory;
    private InternalIndicators indicators;
    private MotorSystem motor;
    private Genetics genetics;
    private ThoughtBubble thoughtBubble;
    private DecisionSystem decisionSystem;
    private InteractionSystem interactionSystem;
    
    private float wanderTimer = 0f;
    private float decisionTimer = 0f;
    private float idleLogTimer = 0f;
    private float memoryLogTimer = 0f;
    [HideInInspector] public float socialCooldownTimer = 0f;
    [HideInInspector] public float attackCooldownTimer = 0f;
    [HideInInspector] public bool isPerformingAction = false;
    [HideInInspector] public InteractableObject HuntedTarget = null;
    
    private InteractableObject lastChosenTarget;
    private Vector2? currentNavTarget = null;
    private ObjectType? currentNavCategory = null;
    private float actionWatchdog = 0f;
    
    void Awake()
    {
        sensors = GetComponent<SensorySystem>();
        endocrine = GetComponent<EndocrineSystem>();
        memory = GetComponent<MemorySystem>();
        indicators = GetComponent<InternalIndicators>();
        motor = GetComponent<MotorSystem>();
        genetics = GetComponent<Genetics>();
        thoughtBubble = GetComponent<ThoughtBubble>();
        decisionSystem = GetComponent<DecisionSystem>();
        interactionSystem = GetComponent<InteractionSystem>();
        
        // MotorSystem теперь вызывает interactionSystem.HandleInteraction напрямую через FireInteraction()
        if (sensors != null && decisionSystem != null) sensors.OnObjectDetected += decisionSystem.LogPrimaryAnalysis;
    }
    
    void Update()
    {
        if (indicators != null && indicators.IsDead) return;
        
        // Сторожевой таймер: если isPerformingAction залипло больше 10 секунд — принудительный сброс
        if (isPerformingAction)
        {
            actionWatchdog += Time.deltaTime;
            if (actionWatchdog > 10f)
            {
                Debug.LogWarning($"[WATCHDOG] {gameObject.name}: isPerformingAction залипло на {actionWatchdog:F1}с! Принудительный сброс.");
                isPerformingAction = false;
                actionWatchdog = 0f;
            }
            return;
        }
        actionWatchdog = 0f;

        if (socialCooldownTimer > 0) 
        {
            socialCooldownTimer -= Time.deltaTime;
            // Одиночество перебарывает желание отдохнуть от общения
            if (indicators.Loneliness > 60f)
            {
                Debug.Log("[Социум] Мне так одиноко, что я больше не хочу быть один!");
                if (thoughtBubble != null) thoughtBubble.ShowThought("Хочу к своим!");
                socialCooldownTimer = 0f;
            }
        }
        
        if (attackCooldownTimer > 0) 
        {
            attackCooldownTimer -= Time.deltaTime;
            if (attackCooldownTimer <= 0 && HuntedTarget != null && HuntedTarget.gameObject.activeInHierarchy)
            {
                InternalIndicators targetInd = HuntedTarget.GetComponent<InternalIndicators>();
                if (targetInd != null && !targetInd.IsDead)
                {
                    float dist = Vector2.Distance(transform.position, HuntedTarget.transform.position);
                    if (dist <= motor.interactionRange + 0.5f && !isPerformingAction)
                    {
                        interactionSystem.HandleInteraction(HuntedTarget);
                    }
                    else if (!isPerformingAction)
                    {
                        motor.MoveToAndInteract(HuntedTarget, 2.5f);
                    }
                }
            }
        }
        
        // Пассивное снижение одиночества в толпе
        if (sensors.SensedObjects.Count > 0)
        {
            bool seesCreature = false;
            foreach (var sensed in sensors.SensedObjects)
            {
                if (sensed.actualObject.type == ObjectType.Creature && sensed.isIdentified)
                {
                    seesCreature = true;
                    break;
                }
            }
            if (seesCreature)
            {
                indicators.Loneliness -= Time.deltaTime * 3f; 
            }
        }

        // 1. Механика СНА
        if (indicators.IsSleeping)
        {
            endocrine.AddEndorphin(Time.deltaTime * 5f); 
            if (indicators.Energy >= 80f)
            {
                Debug.Log("[Сон] Я выспался! Доброе утро.");
                indicators.IsSleeping = false;
                motor.SetSleepVisuals(false); 
            }
            return; 
        }

        // Обморок
        if (indicators.Energy <= 0)
        {
            Debug.Log("[Сон] Упал в обморок от усталости...");
            indicators.IsSleeping = true;
            motor.StopMoving();
            motor.SetSleepVisuals(true); 
            return;
        }

        // Поиск безопасного места перед сном
        if (indicators.Energy < 30f)
        {
            if (!motor.IsMoving)
            {
                bool dangerNearby = false;
                Vector2 dangerPos = Vector2.zero;

                foreach (var sensed in sensors.SensedObjects)
                {
                    if (!sensed.isIdentified) continue; // Силуэтов не боимся так сильно перед сном
                    
                    ExperienceRecord mem = memory.GetExperience(sensed.actualObject.ObjectName);
                    if (sensed.actualObject.type == ObjectType.Danger || mem.deltaPain > 0)
                    {
                        dangerNearby = true;
                        dangerPos = sensed.perceivedPosition;
                        break;
                    }
                }

                if (dangerNearby)
                {
                    Debug.Log("[Сон] Рядом опасность! Отхожу подальше перед сном.");
                    motor.MoveAway(dangerPos, 1f); 
                }
                else
                {
                    Debug.Log("[Сон] Врагов не видно. Ложусь спать прямо тут.");
                    indicators.IsSleeping = true;
                    motor.StopMoving();
                    motor.SetSleepVisuals(true); 
                    return;
                }
            }
        }

        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0)
        {
            decisionTimer = (endocrine != null && endocrine.Aggression > 70f) ? 0.1f : 0.5f; 
            if (sensors.SensedObjects.Count > 0 && decisionSystem != null)
            {
                DecisionResult result = decisionSystem.Evaluate(sensors.SensedObjects, lastChosenTarget);
                if (result.Action == ActionType.Flee)
                {
                    lastChosenTarget = result.Target.actualObject;
                    DecideToAvoid(result.Target);
                }
                else if (result.Action == ActionType.Interact)
                {
                    // Если мы шли к координатам по памяти или учуяли цель (CurrentTarget == null), сразу переключаемся на точное преследование объекта!
                    if (lastChosenTarget != result.Target.actualObject || !motor.IsMoving || motor.CurrentTarget == null)
                    {
                        lastChosenTarget = result.Target.actualObject;
                        DecideToInteract(result.Target);
                    }
                }
                else
                {
                    if (lastChosenTarget != null && motor.IsMoving && lastChosenTarget.gameObject.activeInHierarchy)
                    {
                        // Если мы уже бежим к цели взаимодействия, не сбрасываем бег от случайного скачка очков!
                    }
                    else
                    {
                        if (TryNavigateFromMemory()) return;

                        if (lastChosenTarget != null) motor.StopMoving(); 
                        lastChosenTarget = null;
                        if (sensors.SensedObjects.Count > 0)
                        {
                            if (Time.time > idleLogTimer)
                            {
                                Debug.Log("[Анализ] Вижу объекты, но не хочу с ними взаимодействовать (Слишком сыт или не интересно).");
                                idleLogTimer = Time.time + 3f; 
                            }
                        }
                    }
                }
            }
            else
            {
                if (lastChosenTarget != null && motor.IsMoving && lastChosenTarget.gameObject.activeInHierarchy)
                {
                    // Пропускаем поиск по памяти, если уже активно бежим к выбранной цели
                }
                else
                {
                    // Если рядом никого и ничего нет в датчиках, используем память (для поиска еды или жертвы при голоде)!
                    if (TryNavigateFromMemory()) return;
                }
            }
        }

        // Логика скуки и паники от голода работает только если мы прямо сейчас не ведем охоту, бой или взаимодействие!
        bool hasActiveTarget = (lastChosenTarget != null && lastChosenTarget.gameObject.activeInHierarchy) || 
                               (HuntedTarget != null && HuntedTarget.gameObject.activeInHierarchy) || 
                               attackCooldownTimer > 0 || isPerformingAction;

        if (!motor.IsMoving && !hasActiveTarget)
        {
            indicators.Boredom += Time.deltaTime * 10f;
            if (indicators.Boredom > 20f || indicators.Satiety <= 20f)
            {
                wanderTimer -= Time.deltaTime;
                if (wanderTimer <= 0)
                {
                    if (indicators.Satiety <= 20f)
                    {
                        // Проверяем параметры существа: отчаянные и смелые могут попытаться напасть на кого угодно!
                        float myFear = (genetics != null) ? genetics.Fearfulness : 0.5f;
                        InteractableObject desperateTarget = null;

                        // Ищем цель среди всех сенсоров в зоне восприятия
                        foreach (var sensed in sensors.SensedObjects)
                        {
                            if (sensed.actualObject == null || !sensed.actualObject.gameObject.activeInHierarchy) continue;
                            
                            // Если пугливость близка к минимуму (<= 0.65), от отчаяния мы готовы атаковать даже Хищника (Danger/Hunter)!
                            if (myFear <= 0.65f && sensed.actualObject.type == ObjectType.Danger)
                            {
                                desperateTarget = sensed.actualObject;
                                break; // Хищник найден — отчаянная атака!
                            }
                            // Если пугливость средняя или низкая (<= 1.3), можем напасть на сородича
                            else if (myFear <= 1.3f && sensed.actualObject.type == ObjectType.Creature)
                            {
                                ExperienceRecord mem = memory != null ? memory.GetExperience(sensed.actualObject.ObjectName) : new ExperienceRecord();
                                if (mem.socialBond < 75f) // Не нападаем на близких друзей
                                {
                                    desperateTarget = sensed.actualObject;
                                }
                            }
                        }

                        // Если сенсоры никого не вернули (например, враг чуть за краем угла обзора), делаем круговорой поиск хищников/опасности в радиусе 15!
                        if (desperateTarget == null && myFear <= 0.65f)
                        {
                            Collider2D[] nearbyCols = Physics2D.OverlapCircleAll(transform.position, 15f);
                            foreach (var col in nearbyCols)
                            {
                                InteractableObject io = col.GetComponent<InteractableObject>() ?? col.GetComponentInParent<InteractableObject>();
                                if (io != null && io != this.GetComponent<InteractableObject>() && io.type == ObjectType.Danger)
                                {
                                    desperateTarget = io;
                                    break;
                                }
                            }
                        }

                        if (desperateTarget != null)
                        {
                            Debug.Log($"[Отчаянная охота!] От голода и смелости (пугливость: {myFear:F2}) {gameObject.name} бросается на {desperateTarget.ObjectName} ({desperateTarget.type})!");
                            if (thoughtBubble != null) thoughtBubble.ShowThought("Охота! Либо он, либо я!");
                            HuntedTarget = desperateTarget;
                            lastChosenTarget = desperateTarget;
                            DecideToInteract(new SensedObject { actualObject = desperateTarget, perceivedPosition = desperateTarget.transform.position });
                            wanderTimer = 2.0f; // Даем время добежать до врага!
                        }
                        else
                        {
                            if (indicators.Satiety < 25f)
                            {
                                Debug.Log("[Голод] АВАРИЙНЫЙ ПОИСК ЕДЫ! Память пуста, бегаю в панике!");
                                if (thoughtBubble != null) thoughtBubble.ShowThought("Где же еда?!");
                                motor.MoveToRandomPoint(15f, 2.5f); // Более широкий и быстрый поиск
                                wanderTimer = Random.Range(0.5f, 1f);
                            }
                            else
                            {
                                Debug.Log("[Голод] Ищу еду! Вблизи нет ни пищи, ни подходящих целей для охоты.");
                                motor.MoveToRandomPoint(10f, 2f); // Быстрый бег в радиусе 10
                                wanderTimer = Random.Range(0.5f, 1.5f);
                            }
                        }
                    }
                    else
                    {
                        Debug.Log("[Проактивность] Мне скучно стоять. Пойду бродить...");
                        motor.MoveToRandomPoint(5f, 0.5f);
                        wanderTimer = Random.Range(1f, 3f);
                    }
                }
            }
        }
        else
        {
            indicators.Boredom -= Time.deltaTime * 2f;
            indicators.Boredom = Mathf.Max(0, indicators.Boredom);
        }
    }
    
    private bool TryNavigateFromMemory()
    {
        if (currentNavTarget.HasValue && motor.IsMoving)
        {
            return true; // Продолжаем идти к цели по памяти
        }

        // Если дошли до места, но нужного объекта там нет (иначе он бы попал в EvaluateVisibleObjects и мы бы его выбрали)
        if (currentNavTarget.HasValue && !motor.IsMoving)
        {
            bool actuallyFoundIt = false;
            foreach (var sensed in sensors.SensedObjects)
            {
                if (sensed.actualObject.type == currentNavCategory.Value && sensed.isIdentified)
                {
                    actuallyFoundIt = true;
                    break;
                }
            }
            
            if (actuallyFoundIt)
            {
                // Объект тут есть, но мы с ним не взаимодействуем (напр. он спит). Не забываем и не расстраиваемся!
                currentNavTarget = null;
                currentNavCategory = null;
                return false;
            }

            Debug.Log($"[Память] Пришел на место, но объекта типа {currentNavCategory} тут нет! Забываю...");
            memory.ForgetCategoryLocation(currentNavCategory.Value, currentNavTarget.Value);
            endocrine.AddStress(10f); // Разочарование
            currentNavTarget = null;
            currentNavCategory = null;
            return false;
        }

        if (indicators.Satiety < 75f)
        {
            // 1-й приоритет (Высший): Свежая обычная еда в памяти (не трупы!)
            if (memory.TryGetKnownLocation(ObjectType.Food, out Vector2 pos, name => !name.StartsWith("Труп ") && memory.GetExperience(name).socialBond < 50f))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Я помню, где была свежая еда (яблоко)! Иду туда.");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Помню яблоко!");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.5f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.Food; 
                return true;
            }

            // 2-й приоритет: Фруктовое дерево в памяти
            if (memory.TryGetKnownLocation(ObjectType.FoodSource, out pos))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Я помню, где было фруктовое дерево! Иду туда за плодами.");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Помню дерево!");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.5f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.FoodSource; 
                return true;
            }

            // 3-й приоритет: Неизвестные объекты в памяти (когда нет явной еды, лучше проверить неизвестное, чем есть труп!)
            if (memory.TryGetKnownLocation(ObjectType.Unknown, out pos))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Голод] Знакомой еды нет... Помню какое-то неизвестное место. Иду проверять!");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Что это? Интересно");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.5f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.Unknown;
                return true;
            }

            // 4-й приоритет: Труп незнакомца/врага в памяти (когда нормальной еды и неизведанных мест нет)
            float requiredTabooBond = indicators.Satiety < 20f ? 75f : 50f;
            if (memory.TryGetKnownLocation(ObjectType.Food, out pos, name => name.StartsWith("Труп ") && memory.GetExperience(name.Substring(5)).socialBond < requiredTabooBond))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Яблок нет... Но я помню, где лежал труп! Иду туда.");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Съем труп...");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.5f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.Food; 
                return true;
            }
            
            // 5-й приоритет (Крайний случай): Охота на живых сородичей (каннибализм)
            if ((indicators.Satiety <= GetHuntSatietyThreshold() || endocrine.Aggression > 80f) && (genetics == null || genetics.Fearfulness <= 1.3f))
            {
                float huntTabooBond = indicators.Satiety <= GetHuntSatietyThreshold() ? 85f : 60f;
                if (memory.TryGetKnownLocation(ObjectType.Creature, out pos, name => memory.GetExperience(name).socialBond < huntTabooBond))
                {
                    if (Time.time > memoryLogTimer)
                    {
                        Debug.Log("[Голод] Еды и трупов нет, деревьев нет! Иду охотиться на сородича по памяти!");
                        if (thoughtBubble != null) thoughtBubble.ShowThought("Съем кого-то!");
                        memoryLogTimer = Time.time + 4f;
                    }
                    motor.MoveToPosition(pos, 2.5f);
                    currentNavTarget = pos;
                    currentNavCategory = ObjectType.Creature;
                    return true;
                }
            }
        }

        if (indicators.Satiety >= 45f && indicators.Loneliness > 60f)
        {
            if (memory.TryGetKnownLocation(ObjectType.Creature, out Vector2 pos))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Я помню, где видел сородичей. Иду к ним!");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Ищу своих...");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.2f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.Creature;
                return true;
            }
        }

        if (indicators.Satiety >= 45f && indicators.Boredom > 60f)
        {
            if (memory.TryGetKnownLocation(ObjectType.Toy, out Vector2 pos) || 
                memory.TryGetKnownLocation(ObjectType.Creature, out pos))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Я помню, где были развлечения. Иду туда.");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Туда, где весело");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(pos, 1.0f);
                currentNavTarget = pos;
                currentNavCategory = ObjectType.Toy;
                return true;
            }

            // Если игрушек нет, идем изучать неизведанное!
            if (memory.TryGetKnownLocation(ObjectType.Unknown, out Vector2 unknownPos))
            {
                if (Time.time > memoryLogTimer)
                {
                    Debug.Log("[Память] Скучно. Помню, видел что-то непонятное. Иду проверять!");
                    if (thoughtBubble != null) thoughtBubble.ShowThought("Что же это было?");
                    memoryLogTimer = Time.time + 4f;
                }
                motor.MoveToPosition(unknownPos, 1.2f);
                currentNavTarget = unknownPos;
                currentNavCategory = ObjectType.Unknown;
                return true;
            }
        }

        return false;
    }

    private void DecideToInteract(SensedObject sensed)
    {
        currentNavTarget = null;
        currentNavCategory = null;
        float speedMod = 1f;
        if (endocrine.Curiosity > 50f) speedMod = 1.2f;
        if (indicators.Satiety < 35f) speedMod = 1.5f;
        if (sensed.actualObject != null && IsHostileTowards(sensed.actualObject))
        {
            speedMod = 2.5f; // Хищник и отчаянный охотник всегда переходят на бег при погоне за целью!
        }

        if (!sensed.isIdentified && !(sensed.actualObject != null && sensed.actualObject == HuntedTarget))
        {
            if (thoughtBubble != null) thoughtBubble.ShowThought("Что это там?");
        }
        else if (!sensed.isLocationPrecise && !(sensed.actualObject != null && sensed.actualObject == HuntedTarget))
        {
            if (thoughtBubble != null) thoughtBubble.ShowThought("Чую запах...");
        }
        
        motor.MoveToAndInteract(sensed.actualObject, speedMod);
    }
    
    private void DecideToAvoid(SensedObject sensed)
    {
        DecideToAvoid(sensed.actualObject, sensed.perceivedPosition);
    }
    
    public void DecideToAvoid(InteractableObject obj, Vector2 pos)
    {
        currentNavTarget = null;
        currentNavCategory = null;
        endocrine.AddStress(15f); 
        
        float speedMod = 2f;
        if (endocrine.Fear > 60f) speedMod = 3f;
        
        motor.MoveAway(pos, speedMod);
    }
    
    public float GetHuntSatietyThreshold()
    {
        // Индивидуальный порог охоты на живых: сильные и смелые особи готовы охотиться раньше (при сытости до 45%),
        // пугливые или слабые охотятся только от отчаяния и голода (при сытости <= 25-35%)!
        float threshold = 35f;
        if (genetics != null)
        {
            if (genetics.Strength > 1.1f && genetics.Fearfulness <= 1.0f) threshold = 45f;
            else if (genetics.Fearfulness > 1.2f) threshold = 25f;
        }
        return threshold;
    }

    public bool IsHostileTowards(InteractableObject obj)
    {
        if (obj == null || indicators == null || memory == null || endocrine == null) return false;
        
        // Отчаянная охота на хищников/опасность (в том числе префабы Hunter с типом Danger):
        // если сытость близка к нулю (<= 5) и существо достаточно смелое (Fearfulness <= 0.65f),
        // оно готово отчаянно атаковать даже Опасность/Хищника!
        bool isDesperateHunt = indicators.Satiety <= 5f && (genetics == null || genetics.Fearfulness <= 0.65f);
        if (isDesperateHunt && (obj.type == ObjectType.Danger || obj.type == ObjectType.Creature || obj.ObjectName.Contains("Hunter") || obj.ObjectName.Contains("Охотник")))
        {
            HuntedTarget = obj;
            return true;
        }

        if (obj.type != ObjectType.Creature) return false;
        
        ExperienceRecord mem = memory.GetExperience(obj.ObjectName);
        float huntTabooBond = indicators.Satiety <= GetHuntSatietyThreshold() ? 85f : 60f;
        if (mem.socialBond >= huntTabooBond) return false; // Не охотимся на друзей и семью

        Brain otherBrain = obj.GetComponent<Brain>();
        Genetics otherGenetics = otherBrain != null ? otherBrain.GetComponent<Genetics>() : null;
        
        bool willHunt = isDesperateHunt || genetics == null || genetics.Fearfulness < 1.3f || (otherGenetics != null && genetics.Strength > otherGenetics.Strength);
        if (!willHunt) return false; // Индивидуальность сородичей: слишком пугливые или слабые не решаются нападать на живых, если это не отчаянный бой

        // Если мы голодны (<= порога) или уже ведем активную охоту (до сытости 70%), считаем цель легитимной жертвой!
        bool starvingOrHunting = isDesperateHunt || indicators.Satiety <= GetHuntSatietyThreshold() || (HuntedTarget == obj && indicators.Satiety < 70f) || (lastChosenTarget == obj && indicators.Satiety < 70f);
        bool furious = endocrine.Aggression > 80f;

        bool isHostile = starvingOrHunting || furious;
        if (isHostile) HuntedTarget = obj;
        else if (HuntedTarget == obj && indicators.Satiety >= 70f) HuntedTarget = null;

        return isHostile;
    }
}
