using Unity.VisualScripting;
using UnityEngine;
using System.Collections;

public class PlayerLogic : MonoBehaviour
{
    public float PlayerSpeed = 2.85f;
    public float MaxPlayerSpeed = 5.5f;

    int chetchik=0;
    Vector3 targetPoint;
    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        if (Input.GetMouseButton(1) && chetchik==0)
        {
            Ray rayToPlane = Camera.main.ScreenPointToRay(mousePos);
            Plane plane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            float distance;
            if (plane.Raycast(rayToPlane, out distance))
            {
                targetPoint = rayToPlane.GetPoint(distance);
                transform.position = Vector3.MoveTowards(transform.position, targetPoint, PlayerSpeed * Time.deltaTime);
                chetchik=1;
            }
        }

        if (chetchik!=0)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, PlayerSpeed * Time.deltaTime);
            chetchik=0;
        }

    }
}
