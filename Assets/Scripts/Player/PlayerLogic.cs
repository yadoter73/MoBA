using UnityEngine;
using System.Collections;
using UnityEngine.Events;
public class PlayerLogic : MonoBehaviour
{
    public float PlayerSpeed = 2.85f;
    public float MaxPlayerSpeed = 5.5f;
    public float attackRate = 3f;
    public int attackDamage = 50;
    public UnityEvent OnAttack { get; set; } = new();
    Ray rayToPlane;
    Vector3 targetPoint;

    private int movingState = 0;
    private bool isMoving;
    private float distance;
    private float nextAttackTime = 0f;
    private bool isAttacking = false;
    
    [SerializeField] private float attackRadius = 1.2f;
    [SerializeField] private LayerMask enemyLayer;

    void FixedUpdate()
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
        if (Input.GetMouseButton(0) && !isAttacking)
        {
            StartCoroutine(Attack());
            nextAttackTime = Time.time + 1f / attackRate;
        }
    }
    IEnumerator Attack()
    {
        isAttacking = true;
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, attackRadius, enemyLayer);
        OnAttack?.Invoke();

        foreach (Collider collider in hitColliders)
        {
            EnemyHealth enemy = collider.GetComponent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.TakeDamage(attackDamage);
            }
        }
        yield return new WaitForSeconds(attackRate);

        isAttacking = false;
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}
