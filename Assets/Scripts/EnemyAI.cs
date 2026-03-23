using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("ใส่ Player Transform ที่ต้องการให้ AI วิ่งตาม")]
    public Transform player;

    [Header("Movement")]
    public float moveSpeed = 5f;
    [Tooltip("ความเร็วในการปีน/กระโดด ข้ามสิ่งกีดขวาง")]
    public float climbSpeed = 3f;

    [Header("Combat Settings")]
    public float attackDamage = 20f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;
    
    [Header("Detection Settings")]
    public float detectionRange = 20f;

    private NavMeshAgent agent;
    private PlayerHealth playerHealth;
    private float lastAttackTime;
    private bool isClimbing = false;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        
        // ปิดการกระโดดข้ามลิงก์แบบแปลกๆ ของ Unity ให้เราเขียนโค้ดปีนเอง
        agent.autoTraverseOffMeshLink = false;

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        if (player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        if (player == null || isClimbing) return; // ถ้าปีนอยู่ ไม่ต้องคำนวณเดินธรรมดา

        // อัปเดตความเร็วเผื่อมีการปรับแก้ใน Editor ระหว่างเล่น
        agent.speed = moveSpeed;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            agent.SetDestination(player.position);

            if (distanceToPlayer <= attackRange)
            {
                AttackPlayer();
            }
        }

        // ตรวจสอบว่าถึงจุดที่ต้องปีนหรือกระโดด (OffMeshLink) หรือยัง
        if (agent.isOnOffMeshLink)
        {
            StartCoroutine(ClimbOrJump());
        }
    }

    private IEnumerator ClimbOrJump()
    {
        isClimbing = true;
        OffMeshLinkData data = agent.currentOffMeshLinkData;

        // จุดเริ่มต้น และจุดหมายปลายทางของการปีน
        Vector3 startPos = agent.transform.position;
        Vector3 endPos = data.endPos + Vector3.up * agent.baseOffset;
        
        float journey = 0f;
        while (journey < 1f)
        {
            journey += Time.deltaTime * climbSpeed;
            // สร้างเส้นโค้งให้ดูเหมือนการกระโดด/ปีนข้าม (ใช้พาราโบลา)
            float heightCurve = Mathf.Sin(Mathf.PI * journey); 
            agent.transform.position = Vector3.Lerp(startPos, endPos, journey) + (Vector3.up * heightCurve * 1.5f);
            
            yield return null;
        }

        agent.CompleteOffMeshLink();
        isClimbing = false;
    }

    private void AttackPlayer()
    {
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            agent.isStopped = true;
            transform.LookAt(new Vector3(player.position.x, transform.position.y, player.position.z));

            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }

            lastAttackTime = Time.time;
            Invoke("ResumeAgent", 0.5f);
        }
    }

    private void ResumeAgent()
    {
        if (agent != null && agent.isOnNavMesh && !isClimbing)
        {
            agent.isStopped = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
