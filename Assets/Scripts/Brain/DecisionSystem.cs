using UnityEngine;
using System.Collections.Generic;

public enum ActionType { None, Interact, Flee }
public struct DecisionResult
{
    public ActionType Action;
    public SensedObject Target;
}

public class DecisionSystem : MonoBehaviour
{
    private EndocrineSystem endocrine;
    private MemorySystem memory;
    private InternalIndicators indicators;
    private Genetics genetics;
    private Brain brain; 

    void Awake()
    {
        endocrine = GetComponent<EndocrineSystem>();
        memory = GetComponent<MemorySystem>();
        indicators = GetComponent<InternalIndicators>();
        genetics = GetComponent<Genetics>();
        brain = GetComponent<Brain>();
    }

    public void LogPrimaryAnalysis(SensedObject sensed)
    {
        if (!sensed.isIdentified)
        {
            Debug.Log($"[Блок Анализа] Вижу какой-то силуэт вдали...");
            return;
        }
        
        InteractableObject obj = sensed.actualObject;
        string targetName = obj.ObjectName.StartsWith("Труп ") ? obj.ObjectName.Substring(5) : obj.ObjectName;
        ExperienceRecord mem = memory.GetExperience(targetName);
        float rationalAttraction = (obj.type == ObjectType.Food || obj.type == ObjectType.FoodSource) ? (100f - indicators.Satiety) * 1.5f : 0f;
        
        if (mem.deltaSatiety > 0) rationalAttraction += mem.deltaSatiety * (100f - indicators.Satiety) * 0.02f;
        if (mem.deltaBoredom < 0) rationalAttraction += Mathf.Abs(mem.deltaBoredom) * indicators.Boredom * 0.05f;

        float distance = Vector2.Distance(transform.position, sensed.perceivedPosition);
        float distanceFactor = 1f + (distance * 0.2f); 
        
        float fear = (mem.deltaPain > 0) ? (mem.deltaPain + endocrine.Fear * 0.5f) / distanceFactor : 0f;
        if (obj.type == ObjectType.Danger) fear += (endocrine.Fear * 0.5f) / distanceFactor;

        float curiosity = 0f;
        bool isUnknown = (mem.deltaSatiety == 0 && mem.deltaBoredom == 0 && mem.deltaPain == 0 && mem.socialBond == 0);
        if (isUnknown && endocrine.Curiosity > 30f)
        {
            curiosity = endocrine.Curiosity * 0.5f;
        }

        string posType = sensed.isLocationPrecise ? "Вижу" : "Чую";
        Debug.Log($"[Блок Анализа] {posType} {obj.ObjectName}. Рациональная оценка (Польза): {rationalAttraction:F1}. Эмоциональная: Любопытство {curiosity:F1}, Страх {fear:F1}.");
    }

    public DecisionResult Evaluate(List<SensedObject> sensedObjects, InteractableObject lastChosenTarget)
    {
        SensedObject? bestTargetToInteract = null;
        SensedObject? bestTargetToFlee = null;
        
        float highestInteractScore = 0;
        float highestFleeScore = 0;

        foreach (var sensed in sensedObjects)
        {
            if (sensed.actualObject == null) continue;

            InteractableObject obj = sensed.actualObject;
            float attractionScore = 0;
            float fearScore = 0;
            
            float distance = Vector2.Distance(transform.position, sensed.perceivedPosition);
            float distanceFactor = 1f + (distance * 0.2f);
            bool isHostile = brain != null && brain.IsHostileTowards(obj);

            if (!sensed.isIdentified && !isHostile && !(brain != null && brain.HuntedTarget == obj))
            {
                if (endocrine.Curiosity > 30f)
                {
                    attractionScore += (endocrine.Curiosity * 0.8f) / distanceFactor;
                }
            }
            else
            {
                string targetName = obj.ObjectName.StartsWith("Труп ") ? obj.ObjectName.Substring(5) : obj.ObjectName;
                ExperienceRecord mem = memory.GetExperience(targetName);

                if (obj.type == ObjectType.Food) 
                {
                    float requiredTabooBond = (brain != null && indicators.Satiety <= brain.GetHuntSatietyThreshold()) ? 75f : 50f;
                    if (obj.ObjectName.StartsWith("Труп ") && mem.socialBond >= requiredTabooBond)
                    {
                        attractionScore -= 1000f; // Не едим трупы лучших друзей и членов семьи!
                    }
                    else if (obj.ObjectName.StartsWith("Труп "))
                    {
                        // 3-й приоритет: Труп незнакомца/знакомца. Едят только когда рядом нет свежих яблок и пустые деревья!
                        attractionScore += (100f - indicators.Satiety) * 2.2f + 200f; 
                    }
                    else
                    {
                        // 1-й приоритет (Высший): Свежая обычная еда (яблоки, фрукты на земле).
                        attractionScore += (100f - indicators.Satiety) * 3f + 400f; 
                    }
                }
                else if (obj.type == ObjectType.FoodSource)
                {
                    FruitTree tree = obj.GetComponent<FruitTree>();
                    if (tree == null || tree.HasFruits)
                    {
                        // 2-й приоритет: Фруктовое дерево с плодами. Чистая растительная пища лучше поедания чужих трупов!
                        attractionScore += (100f - indicators.Satiety) * 2.6f + 300f; 
                    }
                }

                if (obj.type == ObjectType.Toy)
                {
                    float expectedFun = obj.funValue / (1f + mem.playSatiety);
                    if (expectedFun > 2f)
                    {
                        attractionScore += indicators.Boredom * 1.5f * (expectedFun / Mathf.Max(1f, obj.funValue));
                    }
                }
                
                bool ignoreCreatureInteraction = false;

                if (obj.type == ObjectType.Creature)
                {
                    Brain otherBrain = obj.GetComponent<Brain>();

                    bool isThreatening = mem.deltaPain > 0 || sensed.hasAggressiveScent || (otherBrain != null && otherBrain.IsHostileTowards(this.GetComponent<InteractableObject>()));

                    if (!isHostile && isThreatening)
                    {
                        // Поведение жертвы и обоняние: Распознаем хищника или его агрессивный запах! Отменяем желание дружить.
                        ignoreCreatureInteraction = true;
                        if (sensed.hasAggressiveScent) endocrine.AddStress(5f); // Резкий запах злости мгновенно бодрит и пугает
                        
                        bool isBraveAndStrong = (genetics != null && otherBrain != null && otherBrain.GetComponent<Genetics>() != null && genetics.Fearfulness < 1.3f && genetics.Strength >= otherBrain.GetComponent<Genetics>().Strength);
                        if (!isBraveAndStrong)
                        {
                            // Пугливая или слабая жертва начинает панически убегать ЗАРАНЕЕ, по запаху или виду!
                            fearScore += 150f + (mem.deltaPain / distanceFactor) + (sensed.hasAggressiveScent ? 50f : 0f);
                            ThoughtBubble tb = brain != null ? brain.GetComponent<ThoughtBubble>() : null;
                            if (tb != null && Random.value < 0.15f) 
                            {
                                tb.ShowThought(sensed.hasAggressiveScent ? "Чую запах злости!" : "Он хочет меня съесть!");
                            }
                        }
                        else
                        {
                            // Сильный и храбрый сородич испытывает стресс/адреналин в ответ и готовится дать отпор!
                            endocrine.AddStress(25f);
                            ThoughtBubble tb = brain != null ? brain.GetComponent<ThoughtBubble>() : null;
                            if (tb != null && sensed.hasAggressiveScent && Random.value < 0.15f) tb.ShowThought("Пахнет врагом...");
                        }
                    }
                    else if (!isHostile && brain.socialCooldownTimer > 0)
                    {
                        ignoreCreatureInteraction = true;
                    }
                    else if (otherBrain != null && !isHostile && (otherBrain.socialCooldownTimer > 0 || otherBrain.GetComponent<InternalIndicators>().IsSleeping))
                    {
                        ignoreCreatureInteraction = true;
                    }
                    else if (!isHostile)
                    {
                        attractionScore += indicators.Loneliness * 1.5f;
                        if (endocrine.Fear > 40f)
                        {
                            attractionScore += endocrine.Fear * 2f; 
                        }
                    }
                }

                if (isHostile)
                {
                    bool isDesperate = (indicators.Satiety <= 5f && (genetics == null || genetics.Fearfulness <= 0.65f));
                    float desperateBonus = isDesperate ? 800f : 0f;

                    if (isDesperate)
                    {
                        endocrine.AddStress(30f); // Выброс адреналина в смертельной схватке от отчаяния
                        ThoughtBubble tb = brain.GetComponent<ThoughtBubble>();
                        if (tb != null && Random.value < 0.2f) tb.ShowThought("Либо он, либо я!");
                    }

                    // 4-й приоритет / Отчаянный бой: Охота на сородича или Охотника (Даже типа Danger!). При отчаянной охоте приоритет взлетает (+800)!
                    attractionScore += (100f - indicators.Satiety) * 3f + endocrine.Aggression * 2f + 300f + desperateBonus; 
                    fearScore = 0f; // Во время охоты страх перед целью отключается!
                }

                float memoryTrust = 1f;
                if (genetics != null && genetics.Intelligence < 100f)
                {
                    float ignoreChance = (100f - genetics.Intelligence) / 100f; 
                    if (Random.value < ignoreChance)
                    {
                        memoryTrust = 0f; 
                    }
                }

                if (mem.deltaSatiety > 0)
                {
                    attractionScore += mem.deltaSatiety * (100f - indicators.Satiety) * 0.02f * memoryTrust;
                }
                if (mem.deltaBoredom < 0 && !ignoreCreatureInteraction && !isHostile)
                {
                    attractionScore += Mathf.Abs(mem.deltaBoredom) * indicators.Boredom * 0.05f * memoryTrust;
                }

                if (!isHostile && (obj.type == ObjectType.Danger || mem.deltaPain > 0 || obj.ObjectName.Contains("Альфа") || obj.ObjectName.Contains("Охотник")))
                {
                    // Опасность и обидчики вызывают сильный долговременный страх, чтобы жертва продолжала панически бежать до истощения!
                    float dangerPenalty = obj.type == ObjectType.Danger ? 600f : 250f;
                    fearScore += dangerPenalty + (mem.deltaPain * 3f) + (endocrine.Fear * 2.5f) + (endocrine.Adrenaline * 3f) / distanceFactor;
                    attractionScore = 0f; // Никакого любопытства к хищнику или врагу!
                }
                
                bool isUnknown = (mem.deltaSatiety == 0 && mem.deltaBoredom == 0 && mem.deltaPain == 0 && mem.socialBond == 0);
                if (endocrine.Curiosity > 30f && isUnknown && !isHostile)
                {
                    attractionScore += endocrine.Curiosity * 0.5f; 
                }

                if (ignoreCreatureInteraction || (indicators.Energy < 30f && obj.type != ObjectType.Food && obj.type != ObjectType.FoodSource && !isHostile))
                {
                    attractionScore = 0f; 
                }

                if (indicators.Satiety < 50f && obj.type != ObjectType.Food && obj.type != ObjectType.FoodSource && !isHostile)
                {
                    attractionScore = 0f; // При голоде прекращаем всякие игры и пустое общение!
                }
            }

            float chaosMultiplier = (genetics != null) ? (100f / genetics.Intelligence) : 1f;
            float chaos = Random.Range(-5f, 5f) * chaosMultiplier;
            
            if (attractionScore > 0) attractionScore += chaos;
            if (fearScore > 0) fearScore += Random.Range(-5f, 5f) * chaosMultiplier;

            if (obj == lastChosenTarget)
            {
                if (attractionScore > 0) attractionScore += 20f;
                if (fearScore > 0) fearScore += 20f;
            }

            if (attractionScore > 20f && attractionScore > highestInteractScore)
            {
                highestInteractScore = attractionScore;
                bestTargetToInteract = sensed;
            }
            if (fearScore > 20f && fearScore > highestFleeScore)
            {
                highestFleeScore = fearScore;
                bestTargetToFlee = sensed;
            }
        }

        DecisionResult result = new DecisionResult { Action = ActionType.None };

        if (highestFleeScore > highestInteractScore && bestTargetToFlee.HasValue)
        {
            result.Action = ActionType.Flee;
            result.Target = bestTargetToFlee.Value;
        }
        else if (highestInteractScore >= highestFleeScore && bestTargetToInteract.HasValue)
        {
            result.Action = ActionType.Interact;
            result.Target = bestTargetToInteract.Value;
        }

        return result;
    }
}
