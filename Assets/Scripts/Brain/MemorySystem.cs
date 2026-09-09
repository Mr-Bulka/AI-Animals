using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ExperienceRecord
{
    public float deltaSatiety;
    public float deltaBoredom;
    public float deltaPain;
    public float socialBond; 
    public float playSatiety; 
}

public struct SpatialRecord
{
    public string objectName;
    public ObjectType category;
    public Vector2 position;
    public float timestamp;
}

public class MemorySystem : MonoBehaviour
{
    private Dictionary<string, ExperienceRecord> objectExperience = new Dictionary<string, ExperienceRecord>();
    private List<SpatialRecord> spatialMemory = new List<SpatialRecord>();
    private Genetics genetics;
    
    public float forgettingRate = 0.5f;

    void Awake()
    {
        genetics = GetComponent<Genetics>();
    }

    void Start()
    {
        genetics = GetComponent<Genetics>();
    }

    public Dictionary<string, ExperienceRecord> GetAllMemories()
    {
        return new Dictionary<string, ExperienceRecord>(objectExperience);
    }

    public List<SpatialRecord> GetAllSpatialRecords()
    {
        return new List<SpatialRecord>(spatialMemory);
    }

    public void CleanUpMissingObjects(List<SensedObject> sensedObjects, Vector2 myPos, float recognitionRadius, Vector2 facingDir, float visionAngle)
    {
        for (int i = spatialMemory.Count - 1; i >= 0; i--)
        {
            var record = spatialMemory[i];
            
            // Проверяем, находится ли записанная позиция в нашем текущем поле зрения
            Vector2 dirToRecord = (record.position - myPos);
            float dist = dirToRecord.magnitude;
            
            // Если позиция находится в пределах ближнего четкого зрения (recognitionRadius) и внутри угла зрения
            if (dist <= recognitionRadius && Vector2.Angle(facingDir, dirToRecord) <= visionAngle / 2f)
            {
                // Ищем этот объект среди того, что мы сейчас видим
                bool objectStillThere = false;
                foreach (var sensed in sensedObjects)
                {
                    if (sensed.actualObject != null && sensed.actualObject.ObjectName == record.objectName)
                    {
                        objectStillThere = true;
                        break;
                    }
                }
                
                // Если мы смотрим прямо на то место, где он должен быть, но его там нет - забываем!
                if (!objectStillThere)
                {
                    Debug.Log($"[Память] Я смотрю на место, где должен быть {record.objectName}, но его там нет! Удаляю из памяти.");
                    spatialMemory.RemoveAt(i);
                }
            }
        }
    }

    public bool IsAreaSafe(Vector2 pos, float safeRadius = 15f)
    {
        foreach (var rec in spatialMemory)
        {
            if (rec.category == ObjectType.Danger && Vector2.Distance(rec.position, pos) < safeRadius)
                return false;
        }
        return true;
    }

    public string GetMostSignificantMemoryPhrase()
    {
        if (objectExperience.Count == 0 && spatialMemory.Count == 0) return "Я пока ничего не знаю о мире!";

        string bestKey = "";
        float maxScore = 0f;
        string phraseTemplate = "Я знаю про {0}!";

        foreach (var kvp in objectExperience)
        {
            ExperienceRecord rec = kvp.Value;
            
            if (rec.deltaPain > maxScore) 
            { 
                maxScore = rec.deltaPain; 
                bestKey = kvp.Key; 
                phraseTemplate = "Осторожно, {0} делает больно!"; 
            }
            if (rec.deltaSatiety > maxScore) 
            { 
                maxScore = rec.deltaSatiety; 
                bestKey = kvp.Key; 
                phraseTemplate = "{0} - это очень вкусно!"; 
            }
            if (Mathf.Abs(rec.deltaBoredom) > maxScore) 
            { 
                maxScore = Mathf.Abs(rec.deltaBoredom); 
                bestKey = kvp.Key; 
                phraseTemplate = "С {0} весело играть!"; 
            }
        }

        // Если знаем, где находится дерево или еда, тоже можем поделиться этим знанием в разговоре!
        foreach (var spatial in spatialMemory)
        {
            if (spatial.category == ObjectType.FoodSource && maxScore < 25f)
            {
                maxScore = 25f;
                bestKey = spatial.objectName;
                phraseTemplate = "Я знаю, где растёт {0}!";
            }
            else if (spatial.category == ObjectType.Food && maxScore < 15f)
            {
                maxScore = 15f;
                bestKey = spatial.objectName;
                phraseTemplate = "Я видел еду ({0})!";
            }
        }

        if (maxScore > 5f && !string.IsNullOrEmpty(bestKey))
        {
            return string.Format(phraseTemplate, bestKey);
        }
        
        return "В этом мире так много интересного...";
    }
    
    public void MergeMemories(Dictionary<string, ExperienceRecord> othersMemories)
    {
        string myName = GetComponent<InteractableObject>() != null ? GetComponent<InteractableObject>().ObjectName : gameObject.name;
        float iqFactor = genetics != null ? (genetics.Intelligence / 100f) : 1f;
        float retainFactor = Mathf.Clamp01(iqFactor);

        foreach (var kvp in othersMemories)
        {
            if (kvp.Key == myName) continue; // Не копируем чужое мнение о самом себе как память о стороннем объекте!
            ExperienceRecord otherRec = kvp.Value;
            otherRec.deltaSatiety *= retainFactor;
            otherRec.deltaBoredom *= retainFactor;
            otherRec.deltaPain *= retainFactor;

            if (!objectExperience.ContainsKey(kvp.Key))
            {
                objectExperience[kvp.Key] = otherRec;
            }
            else
            {
                ExperienceRecord myRec = objectExperience[kvp.Key];
                myRec.deltaSatiety = (myRec.deltaSatiety + otherRec.deltaSatiety) / 2f;
                myRec.deltaBoredom = (myRec.deltaBoredom + otherRec.deltaBoredom) / 2f;
                myRec.deltaPain = (myRec.deltaPain + otherRec.deltaPain) / 2f;
                objectExperience[kvp.Key] = myRec;
            }
        }
    }

    // Обмен знаниями о местоположении объектов (дерева с фруктами, пищи и т.д.)
    public void MergeSpatialMemories(List<SpatialRecord> otherSpatialRecords)
    {
        string myName = GetComponent<InteractableObject>() != null ? GetComponent<InteractableObject>().ObjectName : gameObject.name;
        foreach (var rec in otherSpatialRecords)
        {
            if (rec.objectName == myName) continue; // Не сохраняем свои же координаты как сторонний объект
            
            bool alreadyExists = false;
            for (int i = 0; i < spatialMemory.Count; i++)
            {
                if (spatialMemory[i].objectName == rec.objectName)
                {
                    alreadyExists = true;
                    // Если чужая память о местоположении свежее нашей, обновляем свои данные
                    if (rec.timestamp > spatialMemory[i].timestamp)
                    {
                        spatialMemory[i] = rec;
                    }
                    break;
                }
            }
            if (!alreadyExists)
            {
                RememberLocation(rec.objectName, rec.category, rec.position);
            }
        }
    }

    public static string GetBaseName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        int underscoreIndex = name.IndexOf('_');
        if (underscoreIndex != -1) return name.Substring(0, underscoreIndex);
        return name;
    }

    public void RecordExperience(string objectName, ExperienceRecord delta)
    {
        string baseName = GetBaseName(objectName);
        if (!objectExperience.ContainsKey(baseName))
        {
            objectExperience[baseName] = delta;
        }
        else
        {
            ExperienceRecord old = objectExperience[baseName];
            old.deltaSatiety = (old.deltaSatiety + delta.deltaSatiety) / 2f;
            old.deltaBoredom = (old.deltaBoredom + delta.deltaBoredom) / 2f;
            old.deltaPain = (old.deltaPain + delta.deltaPain) / 2f;
            old.socialBond += delta.socialBond; 
            objectExperience[baseName] = old;
        }
        Debug.Log($"Опыт с {baseName} записан. Сытость: {objectExperience[baseName].deltaSatiety:F1}, Скука: {objectExperience[baseName].deltaBoredom:F1}, Боль: {objectExperience[baseName].deltaPain:F1}");
    }
    
    public void RecordPlayInteraction(string playedObjectName)
    {
        string baseName = GetBaseName(playedObjectName);
        ExperienceRecord current = GetExperience(baseName);
        current.playSatiety += 1.0f; 
        objectExperience[baseName] = current;

        List<string> keys = new List<string>(objectExperience.Keys);
        foreach (var key in keys)
        {
            if (key != baseName)
            {
                ExperienceRecord rec = objectExperience[key];
                if (rec.playSatiety > 0)
                {
                    rec.playSatiety *= 0.9f; 
                    objectExperience[key] = rec;
                }
            }
        }
    }

    // --- ПРОСТРАНСТВЕННАЯ ПАМЯТЬ ---

    public int MaxSpatialCapacity => (genetics != null) ? Mathf.Max(1, (int)(genetics.Intelligence / 10f)) : 5;

    public void RememberLocation(string objName, ObjectType category, Vector2 pos)
    {
        for (int i = 0; i < spatialMemory.Count; i++)
        {
            if (spatialMemory[i].objectName == objName)
            {
                SpatialRecord rec = spatialMemory[i];
                rec.timestamp = Time.time;
                rec.position = pos;
                rec.category = category; // Обновляем категорию (вдруг он умер и стал едой!)
                spatialMemory[i] = rec;
                return;
            }
        }

        spatialMemory.Add(new SpatialRecord { objectName = objName, category = category, position = pos, timestamp = Time.time });

        if (spatialMemory.Count > MaxSpatialCapacity)
        {
            int oldestIndex = 0;
            for (int i = 1; i < spatialMemory.Count; i++)
            {
                if (spatialMemory[i].timestamp < spatialMemory[oldestIndex].timestamp)
                {
                    oldestIndex = i;
                }
            }
            spatialMemory.RemoveAt(oldestIndex);
        }
    }

    public bool TryGetKnownLocation(ObjectType type, out Vector2 pos, System.Func<string, bool> validName = null)
    {
        pos = Vector2.zero;
        float newestTime = -1f;
        bool found = false;

        foreach (var rec in spatialMemory)
        {
            if (rec.category == type && (validName == null || validName(rec.objectName)) && rec.timestamp > newestTime)
            {
                // Если мы ищем НЕ опасность (например, еду или игрушку), обходим стороной места, где недавно видели хищника!
                if (type != ObjectType.Danger && !IsAreaSafe(rec.position, 12f))
                {
                    continue; 
                }

                newestTime = rec.timestamp;
                pos = rec.position;
                found = true;
            }
        }
        return found;
    }

    public void ForgetCategoryLocation(ObjectType type, Vector2 pos)
    {
        for (int i = spatialMemory.Count - 1; i >= 0; i--)
        {
            if (spatialMemory[i].category == type && Vector2.Distance(spatialMemory[i].position, pos) < 2f)
            {
                spatialMemory.RemoveAt(i);
            }
        }
    }

    public void ForgetExactObject(string objName)
    {
        for (int i = spatialMemory.Count - 1; i >= 0; i--)
        {
            if (spatialMemory[i].objectName == objName)
            {
                spatialMemory.RemoveAt(i);
            }
        }
    }

    // ---------------------------------

    public bool HasExperience(string objectName)
    {
        return objectExperience.ContainsKey(GetBaseName(objectName));
    }
    
    public ExperienceRecord GetExperience(string objectName)
    {
        if (objectExperience.TryGetValue(GetBaseName(objectName), out ExperienceRecord value))
        {
            return value;
        }
        return new ExperienceRecord();
    }
    
    void Update()
    {
        if (objectExperience.Count > 0)
        {
            List<string> keys = new List<string>(objectExperience.Keys);
            foreach (var key in keys)
            {
                ExperienceRecord rec = objectExperience[key];
                bool changed = false;

                if (rec.deltaSatiety != 0) { rec.deltaSatiety = Mathf.MoveTowards(rec.deltaSatiety, 0, forgettingRate * Time.deltaTime); changed = true; }
                if (rec.deltaBoredom != 0) { rec.deltaBoredom = Mathf.MoveTowards(rec.deltaBoredom, 0, forgettingRate * Time.deltaTime); changed = true; }
                if (rec.deltaPain != 0) { rec.deltaPain = Mathf.MoveTowards(rec.deltaPain, 0, forgettingRate * Time.deltaTime); changed = true; }
                if (rec.socialBond < 0) { rec.socialBond = Mathf.MoveTowards(rec.socialBond, 0, (forgettingRate * 0.2f) * Time.deltaTime); changed = true; }
                else if (rec.socialBond > 0) { rec.socialBond = Mathf.MoveTowards(rec.socialBond, 0, (forgettingRate * 0.01f) * Time.deltaTime); changed = true; } // Дружба почти не забывается!
                if (rec.playSatiety != 0) { rec.playSatiety = Mathf.MoveTowards(rec.playSatiety, 0, (forgettingRate * 0.1f) * Time.deltaTime); changed = true; }

                if (changed)
                {
                    objectExperience[key] = rec;
                }
            }
        }
    }
}
