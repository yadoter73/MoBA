using UnityEngine;

public class PlayerLogic : MonoBehaviour
{
    public float PlayerSpeed = 2.85f;
    public float MaxPlayerSpeed = 5.5f;

    Ray rayToPlane;
    Vector3 targetPoint;

    private int movingState = 0;
    private bool isMoving;
    private Animator animator;
    private Animation animation;
    private float distance;
    
    void Update()
    {
        Vector3 mousePos = Input.mousePosition;
        if (Input.GetKeyDown(KeyCode.S))
        {
            isMoving = false;
            movingState = 0;
        }
        else if (Input.GetMouseButton(1))
        {
            rayToPlane = Camera.main.ScreenPointToRay(mousePos);
            Plane plane = new Plane(Vector3.up, new Vector3(0, transform.position.y, 0));
            if (plane.Raycast(rayToPlane, out distance))
            {
                targetPoint = rayToPlane.GetPoint(distance);
                movingState = 1;
                isMoving = true;
            }
        }
        if (movingState == 1 && isMoving == true)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPoint, PlayerSpeed * Time.deltaTime);
        }
    }
    void Animations()
    {
        
    }

}
