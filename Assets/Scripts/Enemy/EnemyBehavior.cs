using System;
using System.Collections;
using PlayerMovement;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;


public class EnemyBehavior : MonoBehaviour
{ 
    private NavMeshAgent _agent;
    private Transform _player;
    [SerializeField] private LayerMask isGround, isPlayer;
    [SerializeField] private GameObject[] placesToHide;
    
    //Patrolling
    public Vector3 walkPoint;
    private bool _walkPointSet;
    public float walkPointRange;
    
    //Attacking
    private float timeBetweenAttacks;
    private bool _isAttacking;
    [SerializeField] private float rotationSpeed = 10;
    [SerializeField] private float damage = 50;
    
    //States
    [SerializeField]private float sightRange, attackRange, damageRange;
    private bool playerInSightRange, playerInAttackRange, playerInDamageRange;
    [SerializeField] private float health;

    //Audio
    [SerializeField] private AudioClip[] ghoulActionSounds;
    [SerializeField] private AudioClip[] ghoulFootstepSounds;
    [SerializeField] private AudioClip[] ghoulDamageSounds;
    [SerializeField] private AudioClip ghoulDeath;

    private AudioSource _ghoulAudioSource;
    [SerializeField] private float footstepOffset = 0.5f;
    private float _footstepTimer = 0;
    private float _naturalSoundsTimer = 0;
    public static Action EnemyDown;
    
    //Animation
    private Animation enemyAnimation;


    private bool _isDead;

    private void OnEnable()
    {
        EventManager.OnSafeSpaceTrigger += HideFromPlayer;
    }
    private void OnDisable()
    {
        EventManager.OnSafeSpaceTrigger -= HideFromPlayer;
    }

    
    public void Awake()
    {
        _ghoulAudioSource = GetComponent<AudioSource>();
        _ghoulAudioSource.PlayOneShot(ghoulActionSounds[0]);
        _player = GameObject.FindGameObjectWithTag("Player").transform;
        _agent = GetComponent<NavMeshAgent>();
        placesToHide = GameObject.FindGameObjectsWithTag("WalkPoint");
        
        
        enemyAnimation = gameObject.GetComponent<Animation>();
        enemyAnimation.clip = enemyAnimation.GetClip("Idle");
        enemyAnimation.Play(); 

    }
    
    public void Update()
    {
        playerInSightRange = Physics.CheckSphere(transform.position, sightRange, isPlayer);
        playerInAttackRange = Physics.CheckSphere(transform.position, attackRange, isPlayer);
        playerInDamageRange = Physics.CheckSphere(transform.position, damageRange, isPlayer);
        
        
        //if(!playerInSightRange && !playerInAttackRange) Patrolling();
        if(playerInSightRange && !playerInAttackRange && !_isAttacking) Chase();
        if(playerInSightRange && playerInAttackRange) Attack();
        
    }

    private void Patrol()
    {
        if (!_walkPointSet) 
            SearchWalkPoint();
        
        if (_walkPointSet) 
            _agent.SetDestination(walkPoint);
    
        Vector3 distanceToWalkPoint = transform.position - walkPoint;
    
        if (distanceToWalkPoint.magnitude < 1f)
            _walkPointSet = false;
    
    }
    private void SearchWalkPoint()
    {
        float randomZ = Random.Range(-walkPointRange, walkPointRange);
        float randomX = Random.Range(-walkPointRange, walkPointRange);
    
        walkPoint = new Vector3(transform.position.x + randomX, transform.position.y, transform.position.z + randomZ);
        if (Physics.Raycast(walkPoint, -transform.up, 2f, isGround))
            _walkPointSet = true;
    }


    private void Chase()
    {
        _agent.SetDestination(_player.position);
        enemyAnimation.clip = enemyAnimation.GetClip("Run");
        enemyAnimation.Play();
        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0)
        {
            _ghoulAudioSource.PlayOneShot(ghoulFootstepSounds[Random.Range(0, ghoulFootstepSounds.Length - 1)],0.2f);
            _footstepTimer = footstepOffset;
        }
        
    }

    private void Attack()
        {
            _agent.SetDestination(transform.position);
            transform.LookAt(_player);
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, transform.eulerAngles.z);
            timeBetweenAttacks = enemyAnimation.GetClip("Attack2").length;
        
            if (!_isAttacking)
            {
                _isAttacking = true;
                enemyAnimation.clip = enemyAnimation.GetClip("Attack2");
                enemyAnimation.Play();
                _ghoulAudioSource.PlayOneShot(ghoulActionSounds[1]);
            
                //?????????????? LegacyAnimation ??????????????????????. ???? ???????????????? - ???????????????????????????? ?? custom ???????????????? ?????????? ?? ????????????????.
                float timeToDamageAnimation = 1f;
                
                if(!_isDead) 
                    Invoke(nameof(DeliverDamageByAnimation),timeToDamageAnimation);
            
                Invoke(nameof(ResetAttack),timeBetweenAttacks);
            }
        }
    
    private void DeliverDamageByAnimation()
        {
            if(playerInDamageRange && !_isDead)
                FirstPersonController.OnTakeDamage(damage);
        }
    private void ResetAttack()
    {
        _isAttacking = false;
    }
    public void TakeDamage(float damage)
    {
        health -= damage;
        _ghoulAudioSource.PlayOneShot(ghoulDamageSounds[Random.Range(0, ghoulDamageSounds.Length - 1)],2);
        
        if (health < 0)
        {
            _isDead = true;
           StartCoroutine(GhoulDeath());
        }
    }

    private IEnumerator GhoulDeath()
    {
        
        sightRange = 0;
        gameObject.GetComponent<Collider>().enabled = false;
        _ghoulAudioSource.PlayOneShot(ghoulDeath);
        enemyAnimation.clip = enemyAnimation.GetClip("Death");
        enemyAnimation.Play();
        yield return new WaitForSeconds(enemyAnimation.clip.length);
        gameObject.GetComponentInChildren<ParticleSystem>().Play();
        gameObject.GetComponentInChildren<SkinnedMeshRenderer>().enabled = false;
        yield return new WaitForSeconds(1.5f);
        DestroyEnemy();
    }

    private void DestroyEnemy()
    {
        EnemyDown?.Invoke();
        Destroy(gameObject);
    }
    private void HideFromPlayer()
    {
        _ghoulAudioSource.PlayOneShot(ghoulActionSounds[2]);
        enemyAnimation.clip = enemyAnimation.GetClip("Run");
        enemyAnimation.PlayQueued("Run");
        sightRange = 0;
        Vector3 closestWaypoint = FindClosestWayPoint();
        _agent.SetDestination(closestWaypoint);
        Invoke(nameof(DestroyEnemy),9f);
    }
    Vector3 FindClosestWayPoint()
    {
        Vector3 closestWayPoint = default;
        float minDistance = Single.MaxValue;
        foreach (var waypoint in placesToHide)
        {
            if (Vector3.Distance(transform.position, waypoint.transform.position) < minDistance)
            {
                closestWayPoint = waypoint.transform.position;
                minDistance = Vector3.Distance(transform.position, waypoint.transform.position);
            }
        }
        return closestWayPoint;
    }

    public void ActivateEnemy()
    {
        _ghoulAudioSource.PlayOneShot(ghoulActionSounds[0]);
        sightRange = 20;
    }
    
    private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, sightRange);
        }
}
