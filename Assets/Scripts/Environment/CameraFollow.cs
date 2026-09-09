using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Tooltip("Перетащите сюда ваше существо (Creature) из окна Hierarchy")]
    public Transform target;
    
    [Tooltip("Скорость следования камеры. Чем меньше, тем более плавное движение.")]
    public float smoothSpeed = 5f;
    
    [Tooltip("Смещение камеры относительно существа (Z должно быть отрицательным)")]
    public Vector3 offset = new Vector3(0, 0, -10f);

    void LateUpdate()
    {
        if (target != null)
        {
            // Желаемая позиция камеры
            Vector3 desiredPosition = target.position + offset;
            
            // Плавный переход от текущей позиции к желаемой
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            
            // Применяем новую позицию
            transform.position = smoothedPosition;
        }
    }
}
