using UnityEngine;

public class Genetics : MonoBehaviour
{
    [Header("Интеллект")]
    [Tooltip("Влияет на то, насколько существо доверяет своей памяти (50 - глупое, 150 - гений)")]
    [Range(50, 150)] public float Intelligence = 100f;

    [Header("Черты характера (Множители)")]
    [Tooltip("Физическая сила (влияет на максимальное здоровье, урон в бою и стрясывание яблок)")]
    [Range(0.5f, 2f)] public float Strength = 1f;

    [Tooltip("Насколько сильно существо боится опасностей")]
    [Range(0.5f, 1.5f)] public float Fearfulness = 1f;

    [Tooltip("Насколько сильно существо интересуется новым (дофамин)")]
    [Range(0.5f, 1.5f)] public float Playfulness = 1f;

    [Tooltip("Насколько существо нуждается в социуме (усиливает окситоцин)")]
    [Range(0.5f, 1.5f)] public float Sociability = 1f;

    void Start()
    {
        // При желании можно раскомментировать, чтобы характер задавался случайно при рождении
        /*
        Intelligence = Random.Range(70f, 130f);
        Strength = Random.Range(0.7f, 1.3f);
        Fearfulness = Random.Range(0.7f, 1.3f);
        Playfulness = Random.Range(0.7f, 1.3f);
        Sociability = Random.Range(0.7f, 1.3f);
        */
    }
}
