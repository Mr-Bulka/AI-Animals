using UnityEngine;
using UnityEngine.UI; // Используем базовый UI Unity для простоты настройки

public class CreatureUI : MonoBehaviour
{
    [Header("Главная панель интерфейса (фон)")]
    public GameObject mainPanel;

    private InternalIndicators indicators;
    private EndocrineSystem endocrine;
    private Genetics genetics;
    private InteractableObject interactableInfo;
    private MemorySystem memorySystem;

    [Header("Текстовый элемент для всех данных")]
    public Text uiText;

    void Start()
    {
        // По умолчанию ищем компоненты на себе, если они не заданы 
        // (на случай если UI висит прямо на существе, как было раньше)
        if (indicators != null && genetics == null)
        {
            genetics = indicators.GetComponent<Genetics>();
        }
        
        // Скрываем панель при старте игры
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }
    }

    public void BindToCreature(GameObject creature)
    {
        if (creature == null) return;
        
        indicators = creature.GetComponent<InternalIndicators>();
        endocrine = creature.GetComponent<EndocrineSystem>();
        genetics = creature.GetComponent<Genetics>();
        interactableInfo = creature.GetComponent<InteractableObject>();
        memorySystem = creature.GetComponent<MemorySystem>();

        if (mainPanel != null)
        {
            mainPanel.SetActive(true);
        }
    }

    public void ClearBinding()
    {
        indicators = null;
        endocrine = null;
        genetics = null;
        interactableInfo = null;
        memorySystem = null;
        
        if (mainPanel != null)
        {
            mainPanel.SetActive(false);
        }
    }

    private string GetBar(float value, float max = 100f, int length = 10)
    {
        int filled = Mathf.RoundToInt((value / max) * length);
        filled = Mathf.Clamp(filled, 0, length);
        int empty = length - filled;
        return new string('█', filled) + new string('░', empty);
    }

    void Update()
    {
        if (indicators != null && endocrine != null)
        {
            if (uiText != null)
            {
                string stateStr = indicators.IsDead ? " <color=red>[МЕРТВ]</color>" : 
                                  (indicators.IsSleeping ? " <color=cyan>[СПИТ Zzz...]</color>" : "");
                
                string geneticsStr = "";
                if (genetics != null)
                {
                    geneticsStr = $"\n<color=#98FB98><b>--- ГЕНЕТИКА ---</b></color>\n" +
                                  $"IQ: {Mathf.Round(genetics.Intelligence)}\n" +
                                  $"Сила: {genetics.Strength:F1}x\n" +
                                  $"Трусливость: {genetics.Fearfulness:F1}x\n" +
                                  $"Игривость: {genetics.Playfulness:F1}x\n" +
                                  $"Общительность: {genetics.Sociability:F1}x\n";
                }
                
                string nameStr = interactableInfo != null ? $"<b>Имя:</b> <color=#FFFFFF>{interactableInfo.ObjectName}</color>\n" : "";

                string memoryStr = "";
                if (memorySystem != null)
                {
                    memoryStr = $"\n<color=#FFA500><b>--- ПАМЯТЬ ---</b></color>\n";
                    foreach (var kvp in memorySystem.GetAllMemories())
                    {
                        var exp = kvp.Value;
                        string rel = "Нейтрально";
                        if (exp.socialBond > 10f) rel = "<color=#FF69B4>Друг</color>";
                        else if (exp.socialBond < -10f) rel = "<color=#FF0000>Враг</color>";
                        else if (exp.deltaBoredom < -10f) rel = "<color=#00FFFF>Весело</color>";
                        else if (exp.deltaSatiety > 10f) rel = "<color=#00FF00>Еда</color>";
                        else if (exp.deltaPain > 10f) rel = "<color=#FF0000>Опасно</color>";
                        
                        memoryStr += $"{kvp.Key}: {rel} (Связь:{Mathf.Round(exp.socialBond)} Скука:{Mathf.Round(exp.deltaBoredom)})\n";
                    }
                    
                    var spatial = memorySystem.GetAllSpatialRecords();
                    if (spatial.Count > 0)
                    {
                        memoryStr += "\n<b>Пространственная:</b>\n";
                        foreach (var sr in spatial)
                        {
                            memoryStr += $"• <color=#FFFF99>{sr.objectName}</color> -> ({Mathf.Round(sr.position.x)}, {Mathf.Round(sr.position.y)})\n";
                        }
                    }
                }

                uiText.text = 
                    nameStr +
                    $"<color=#FFD700><b>--- ПОТРЕБНОСТИ ---</b></color>{stateStr}\n" +
                    $"Здоровье:\t<color=#FF5555>{GetBar(indicators.Health)}</color> {Mathf.Round(indicators.Health)}\n" +
                    $"Сытость:\t<color=#FFAA00>{GetBar(indicators.Satiety)}</color> {Mathf.Round(indicators.Satiety)}\n" +
                    $"Энергия:\t<color=#55FFFF>{GetBar(indicators.Energy)}</color> {Mathf.Round(indicators.Energy)}\n" +
                    $"Скука:\t\t<color=#AAAAAA>{GetBar(indicators.Boredom)}</color> {Mathf.Round(indicators.Boredom)}\n" +
                    $"Одиночество:\t<color=#FF55FF>{GetBar(indicators.Loneliness)}</color> {Mathf.Round(indicators.Loneliness)}\n" +
                    $"Боль:\t\t<color=#FF0000>{GetBar(indicators.Pain)}</color> {Mathf.Round(indicators.Pain)}\n\n" +
                    $"<color=#00FFFF><b>--- ЭМОЦИИ ---</b></color>\n" +
                    $"Страх:\t\t{Mathf.Round(endocrine.Fear)}\n" +
                    $"Любопытство:\t{Mathf.Round(endocrine.Curiosity)}\n" +
                    $"Спокойствие:\t{Mathf.Round(endocrine.Calmness)}\n" +
                    $"Агрессия:\t{Mathf.Round(endocrine.Aggression)}\n\n" +
                    $"<color=#FF69B4><b>--- ГОРМОНЫ ---</b></color>\n" +
                    $"Адреналин:\t{Mathf.Round(endocrine.Adrenaline)}\n" +
                    $"Кортизол:\t{Mathf.Round(endocrine.Cortisol)}\n" +
                    $"Серотонин:\t{Mathf.Round(endocrine.Serotonin)}\n" +
                    $"Дофамин:\t{Mathf.Round(endocrine.Dopamine)}\n" +
                    $"Мелатонин:\t{Mathf.Round(endocrine.Melatonin)}\n" +
                    $"Эндорфин:\t{Mathf.Round(endocrine.Endorphin)}\n" +
                    $"Окситоцин:\t{Mathf.Round(endocrine.Oxytocin)}\n" +
                    geneticsStr + memoryStr;
            }
        }
    }
}
