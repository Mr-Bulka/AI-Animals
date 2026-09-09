using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ThoughtBubble : MonoBehaviour
{
    [Tooltip("Текстовый компонент для вывода мыслей")]
    public Text bubbleText;
    
    [Tooltip("Объект-контейнер (например, Canvas или подложка облачка)")]
    public GameObject bubbleContainer;

    void Start()
    {
        if (bubbleContainer != null)
        {
            bubbleContainer.SetActive(false); // Скрываем при старте
        }
    }

    public void ShowThought(string text, float duration = 3f)
    {
        if (bubbleContainer == null || bubbleText == null) return;
        
        StopAllCoroutines();
        bubbleContainer.SetActive(true);
        bubbleText.text = text;
        StartCoroutine(HideAfter(duration));
    }

    private IEnumerator HideAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (bubbleContainer != null)
        {
            bubbleContainer.SetActive(false);
        }
    }
}
