using UnityEngine;

public enum ObjectType
{
    Food,
    Danger,
    Toy,
    Creature,
    FoodSource,
    Unknown
}

public class InteractableObject : MonoBehaviour
{
    public string ObjectName = "Unknown";
    public ObjectType type;
    public float nutritionValue = 0f;
    public float painValue = 0f;
    public float funValue = 0f;

    void Awake()
    {
        if (ObjectName == "Unknown" || ObjectName == "Apple" || ObjectName == "Tree" || ObjectName == "Ball" || ObjectName == "Food" || ObjectName == "Danger" || ObjectName == "Hunter" || ObjectName == "Creature" || string.IsNullOrWhiteSpace(ObjectName) || !ObjectName.Contains("_"))
        {
            int uniqueId = Random.Range(1000, 10000);
            switch (type)
            {
                case ObjectType.Food:
                    ObjectName = "Яблоко_" + uniqueId;
                    break;
                case ObjectType.FoodSource:
                    ObjectName = "Дерево_" + uniqueId;
                    break;
                case ObjectType.Toy:
                    ObjectName = "Мяч_" + uniqueId;
                    break;
                case ObjectType.Danger:
                    ObjectName = "Охотник_" + uniqueId;
                    break;
                case ObjectType.Creature:
                    string[] prefixes = { "Боб", "Алиса", "Рекс", "Морт", "Зог", "Луна", "Тор", "Рик", "Морти", "Сэм" };
                    ObjectName = $"{prefixes[Random.Range(0, prefixes.Length)]}_{uniqueId}";
                    break;
            }
            gameObject.name = ObjectName;
        }

        // Если забыли указать питательность для еды в Инспекторе, назначаем стандартное сытое значение
        if (type == ObjectType.Food && nutritionValue <= 0f)
        {
            nutritionValue = 35f; 
        }
        if (type == ObjectType.Toy && funValue <= 0f)
        {
            funValue = 40f;
        }
    }
}
