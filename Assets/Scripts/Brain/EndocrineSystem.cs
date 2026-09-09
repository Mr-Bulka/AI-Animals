using UnityEngine;

public class EndocrineSystem : MonoBehaviour
{
    [Header("Hormones")]
    [Range(0, 100)] public float Adrenaline = 0f;
    [Range(0, 100)] public float Cortisol = 0f;
    [Range(0, 100)] public float Serotonin = 50f;
    [Range(0, 100)] public float Dopamine = 50f;
    [Range(0, 100)] public float Oxytocin = 0f;
    [Range(0, 100)] public float Melatonin = 0f;
    [Range(0, 100)] public float Endorphin = 0f;

    [Header("Emotions (Read Only)")]
    public float Fear => Mathf.Clamp((Adrenaline + Cortisol - (Endorphin * 0.5f) - Oxytocin) * (genetics != null ? genetics.Fearfulness : 1f), 0, 100);
    // Сонливость подавляет любопытство, а Скука — наоборот, разгоняет его! Умножается на Игривость.
    public float Curiosity => Mathf.Clamp((Dopamine + Adrenaline - Melatonin + (indicators != null ? indicators.Boredom : 0f)) * (genetics != null ? genetics.Playfulness : 1f), 0, 100); 
    public float Calmness => Mathf.Clamp(Serotonin - Cortisol + Endorphin + Oxytocin, 0, 100);

    // Агрессия: растет от стресса и голода, падает от эмпатии и счастья. Обратно пропорциональна Трусливости.
    public float Aggression => Mathf.Clamp((Adrenaline + Cortisol - Oxytocin - Serotonin + (100f - (indicators != null ? indicators.Satiety : 100f))) * (genetics != null ? Mathf.Max(0.1f, 2f - genetics.Fearfulness) : 1f), 0, 100);

    private InternalIndicators indicators;
    private Genetics genetics;

    void Start()
    {
        indicators = GetComponent<InternalIndicators>();
        genetics = GetComponent<Genetics>();
    }

    void Update()
    {
        // Нормализация гормонов со временем (гомеостаз)
        Adrenaline = Mathf.Lerp(Adrenaline, 0, Time.deltaTime * 0.5f);
        Cortisol = Mathf.Lerp(Cortisol, 0, Time.deltaTime * (0.1f + Endorphin * 0.01f)); // Эндорфин ускоряет спад кортизола
        Serotonin = Mathf.Lerp(Serotonin, 50f, Time.deltaTime * 0.1f);
        Dopamine = Mathf.Lerp(Dopamine, 50f, Time.deltaTime * 0.1f);
        Oxytocin = Mathf.Lerp(Oxytocin, 0, Time.deltaTime * 0.1f);
        Endorphin = Mathf.Lerp(Endorphin, 0, Time.deltaTime * 0.1f);
        
        if (indicators != null)
        {
            Melatonin = 100f - indicators.Energy; // Чем меньше энергии, тем больше мелатонина
            
            if (indicators.Satiety <= 0f)
            {
                AddStress(Time.deltaTime * 10f); // Голодание вызывает панику и стресс
            }
        }
    }
    
    public void AddStress(float amount)
    {
        // Эндорфин режет получаемый стресс (до 50% при максимальном эндорфине)
        float actualAmount = amount * (1f - (Endorphin / 200f));
        Adrenaline = Mathf.Clamp(Adrenaline + actualAmount, 0, 100);
        Cortisol = Mathf.Clamp(Cortisol + actualAmount * 0.5f, 0, 100);
    }
    
    public void AddPleasure(float amount)
    {
        Dopamine = Mathf.Clamp(Dopamine + amount, 0, 100);
        Serotonin = Mathf.Clamp(Serotonin + amount * 0.5f, 0, 100);
    }
    
    public void AddEndorphin(float amount)
    {
        Endorphin = Mathf.Clamp(Endorphin + amount, 0, 100);
    }

    public void AddOxytocin(float amount)
    {
        float multiplier = (genetics != null) ? genetics.Sociability : 1f;
        Oxytocin = Mathf.Clamp(Oxytocin + (amount * multiplier), 0, 100);
    }
}
