using UnityEngine;

public class PlayerLogic : MonoBehaviour
{
    public float PlayerSpeed = 2.85f;
    public float MaxPlayerSpeed = 5.5f;

    Ray rayToPlane;
    int chek = 0;
    Vector3 targetPoint;
    float distance;
    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        if (Input.GetMouseButton(1))
        {
            rayToPlane = Camera.main.ScreenPointToRay(mousePos);
            Plane plane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (plane.Raycast(rayToPlane, out distance))
            {
                targetPoint = rayToPlane.GetPoint(distance);
                chek = 1;
            }
        }
        if (chek == 1)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, PlayerSpeed * Time.deltaTime);
        }
    }
}