using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using FSM.EventFSM;
using FSM.State;
using FSM.StateConfigurer;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Threading;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyStates { IDLE, PATROL, CHASE, ATTACK, REST }
    private EventFSM<EnemyStates> _fsm;
    private Rigidbody _rb;
    private Renderer _renderer;
    public SpatialGrid myGrid;
    
    [Header("GENERAL STATS")]
    [SerializeField] private List<GridEntity> targets;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float energy;
    private float maxEnergy;

    [Header("STATUS MATERIALS")] 
    [SerializeField] private Material idleMat;
    [SerializeField] private Material patrolMat;
    [SerializeField] private Material chaseMat;
    [SerializeField] private Material attackMat;
    [SerializeField] private Material restMat;
    
    
    [Header("PATROL PROPERTIES")] 
    [SerializeField] private List<GameObject> allWaypoints;
    [SerializeField] private int currentWaypoint;
    [SerializeField] private float patrolSpeed;
    [SerializeField] private float minPatrolDistance;
    [SerializeField] private float energyPatrolMultiplier;
    [SerializeField] private float patrolRange;
    private List<GridEntity> acceptableBoids;
    private int waypointSign = 1;
    
    [Header("CHASE PROPERTIES")]
    [SerializeField] private float minChaseDistance;
    [SerializeField] private float energyChaseMultiplier;
    [SerializeField] private float chaseSpeed;
    private GridEntity currentTarget;

    [Header("ATTACK PROPERTIES")] 
    [SerializeField] private float minAttackDistance;
    [SerializeField] private float attackDuration;

    [Header("REST PROPERTIES")] 
    [SerializeField] private float regenMultiplier;

    [Header("GRID PROPERTIES")] 
    [SerializeField] private float queryLenght;
    
    private void Awake()
    {
        //IA2-P3
        
        #region  CONFIGURATION
        
        maxEnergy = energy;
        _rb = GetComponent<Rigidbody>();
        _renderer = GetComponent<Renderer>();
        
        var idle = new State<EnemyStates>("IDLE");
        var patrol  = new State<EnemyStates>("PATROL");
        var chase = new State<EnemyStates>("CHASE");
        var attack = new State<EnemyStates>("ATTACK");
        var rest = new State<EnemyStates>("REST");
        
        
        StateConfigurer.Create(idle)
            .SetTransition(EnemyStates.PATROL, patrol)
            .SetTransition(EnemyStates.CHASE, chase)
            .SetTransition(EnemyStates.ATTACK, attack)
            .SetTransition(EnemyStates.REST, rest)
            .Done();

        StateConfigurer.Create(patrol)
            .SetTransition(EnemyStates.CHASE, chase)
            .SetTransition(EnemyStates.REST, rest)
            .Done();

        StateConfigurer.Create(chase)
            .SetTransition(EnemyStates.PATROL, patrol)
            .SetTransition(EnemyStates.REST, rest)
            .SetTransition(EnemyStates.ATTACK, attack)
            .Done();
        
        StateConfigurer.Create(attack)
            .SetTransition(EnemyStates.REST, rest)
            .SetTransition(EnemyStates.IDLE, idle)
            .Done();

        StateConfigurer.Create(rest)
            .SetTransition(EnemyStates.IDLE, idle)
            .Done();
        #endregion

        #region IDLE SETUP
        
        idle.OnEnter += x =>
        {
            _renderer.material = idleMat;
            if (energy < 0)
            {
                SendInputToFSM(EnemyStates.REST);
                return;
            }
            
            /*if (Vector3.Distance(transform.position, targets.transform.position) <= minChaseDistance &&
                IsInSight(transform.position, targets.transform.position))
            {
                SendInputToFSM(EnemyStates.CHASE);
                return;
            }
            
            Debug.Log("IDLE!");*/
            
            SendInputToFSM(EnemyStates.PATROL);
        };

        
        #endregion
        
        #region PATROL SETUP

        patrol.OnEnter += x =>
        {
            _renderer.material = patrolMat;
            
            currentWaypoint = GetClosestPatrolPoint(transform, allWaypoints);
            Debug.Log("PATROL!");
        };

        patrol.OnUpdate += () =>
        {
            //IA2-P1
            acceptableBoids = BoidManager.instance.allBoids.Aggregate(FList.Create<GridEntity>(), (flist, boid) =>
            {
                flist = !boid.amDead && boid.CheckDistance(transform.position) <= patrolRange
                    ? flist + boid
                    : flist;
                return flist;
            }).OrderBy(b => b.CheckDistance(transform.position)).ToList();

            foreach (var boid in acceptableBoids)
            {
                if (GetDistance(transform.position, boid.transform.position) <= minChaseDistance &&
                    IsInSight(transform.position, boid.transform.position))
                {
                    SendInputToFSM(EnemyStates.CHASE);
                    return;
                }
            }
            
            Vector3 direction = allWaypoints[currentWaypoint].transform.position - transform.position;
            transform.forward = direction;

            transform.position += transform.forward * patrolSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, allWaypoints[currentWaypoint].transform.position) <=
                minPatrolDistance)
            {
                currentWaypoint += waypointSign;

                if (currentWaypoint < 0 || currentWaypoint == allWaypoints.Count)
                {
                    waypointSign *= -1;
                    currentWaypoint += waypointSign;
                }
            }

            energy -= Time.deltaTime * energyPatrolMultiplier;
            
            if (energy <= 0)
            {
                SendInputToFSM(EnemyStates.REST);
            }
        };

        #endregion
        
        #region CHASE SETUP

        chase.OnEnter += x =>
        {
            _renderer.material = chaseMat; 
            
            Debug.Log("CHASE!");
            
            //IA2-P1
            //IA2-P2
            
            GridEntity nearestTarget = myGrid.GetHashValues().SelectMany(boid => boid).Where(boid => !boid.amDead)
                .OrderBy(boid => GetDistance(boid.transform.position, transform.position)).FirstOrDefault();

            targets = myGrid.GetHashValues().SkipWhile(boidSet => !boidSet.Contains(nearestTarget)).FirstOrDefault()
                .Select(boid => boid).Where(boid => !boid.amDead).ToList();

            if (targets.Count <= 0)
            {
                SendInputToFSM(EnemyStates.PATROL);
                return;
            }

            foreach (var boid in targets)
            {
                boid.SetTarget();
            }
        };

        chase.OnUpdate += () =>
        {
            CheckForNulls();

            if (targets.Count <= 0)
            {
                Debug.Log("ME VOY DE CHASE 0");
                SendInputToFSM(EnemyStates.PATROL);
            }
            
            //IA2-P1
            currentTarget = targets.OrderBy(boid => GetDistance(boid.transform.position, transform.position))
                .FirstOrDefault();

            if (GetDistance(currentTarget.transform.position, transform.position) >= minChaseDistance + 7f)
            {
                Debug.Log("ME VOY DE CHASE 1");
                SendInputToFSM(EnemyStates.PATROL);
            }

            Vector3 direction = currentTarget.transform.position - transform.position;

            transform.forward = direction.normalized;
            transform.position += transform.forward * chaseSpeed * Time.deltaTime;

            if (GetDistance(transform.position, currentTarget.transform.position) <= minAttackDistance && !currentTarget.amDead)
            {
                Debug.Log("ME VOY DE CHASE 2");
                SendInputToFSM(EnemyStates.ATTACK);
                return;
            }
            
            energy -= energyChaseMultiplier * Time.deltaTime;
            
            if (energy < 0)
            {
                Debug.Log("ME VOY DE CHASE 3");
                SendInputToFSM(EnemyStates.REST);
            }
        };

        chase.OnExit += x =>
        {
            foreach (var boid in BoidManager.instance.allBoids)
            {
                if (!boid.amDead)
                    boid.SetNormal();
            }
        };

        #endregion
        
        #region ATTACK SETUP

        attack.OnEnter += x =>
        {
            _renderer.material = attackMat;
            Debug.Log("TIRITO");
            currentTarget.SetDead();
            currentTarget = null;

            SendInputToFSM(EnemyStates.IDLE);
        };

        #endregion

        #region REST SETUP

        rest.OnEnter += x => { Debug.Log("REST!"); _renderer.material = restMat; };
        
        rest.OnUpdate += () =>
        {
            energy += Time.deltaTime * regenMultiplier;
            
            if(energy >= maxEnergy)
                SendInputToFSM(EnemyStates.IDLE);
        };

        #endregion
        
        _fsm = new EventFSM<EnemyStates>(patrol);
    }

    private void Update()
    {
        _fsm.Update();
    }

    private void FixedUpdate()
    {
        _fsm.FixedUpdate();
    }

    public bool IsInSight(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        
        if (!Physics.Raycast(start, direction, direction.magnitude, obstacleMask))
        { 
            return true;
        }

        return false;
    }

    public int GetClosestPatrolPoint(Transform point, List<GameObject> waypoints)
    {
        //IA2-P1
        GameObject rangedWaypoints = waypoints.Select(w => w)
            .Where(w => Vector3.Distance(w.transform.position, point.position) <= patrolRange)
            .OrderBy(w => Vector3.Distance(w.transform.position, point.position)).FirstOrDefault();

        for (int i = 0; i < allWaypoints.Count; i++)
        {
            if (allWaypoints[i] == rangedWaypoints)
            {
                return i;
            }
        }
        
        return -1;
    }
    
    private void SendInputToFSM(EnemyStates state)
    {
        _fsm.SendInput(state);
    }

    public float GetDistance(Vector3 pos1, Vector3 pos2)
    {
        return Vector3.Distance(pos1, pos2);
    }

    public void CheckForNulls()
    {
        if (targets.Count <= 0)
        {
            return;
        }

        List<GridEntity> auxList = new List<GridEntity>(targets);

        foreach (var gridEntity in auxList)
        {
            if (gridEntity == null)
            {
                targets.Remove(gridEntity);
            }
        }
    }
}
