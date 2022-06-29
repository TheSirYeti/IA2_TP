using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using FSM.EventFSM;
using FSM.State;
using FSM.StateConfigurer;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;
using Vector3 = UnityEngine.Vector3;

public class EnemyPatrol : MonoBehaviour
{
    public enum EnemyStates { IDLE, PATROL, CHASE, ATTACK, REST }
    private EventFSM<EnemyStates> _fsm;
    private Rigidbody _rb;
    [Header("GENERAL STATS")]
    [SerializeField] private GameObject target;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private float energy;
    private float maxEnergy;
    
    [Header("IDLE PROPERTIES")]
    

    [Header("PATROL PROPERTIES")] 
    [SerializeField] private List<GameObject> allWaypoits;
    [SerializeField] private int currentWaypoint;
    [SerializeField] private float patrolSpeed;
    [SerializeField] private float minPatrolDistance;
    [SerializeField] private float energyPatrolMultiplier;
    private int waypointSign = 1;
    
    
    [Header("CHASE PROPERTIES")]
    [SerializeField] private float minChaseDistance;
    [SerializeField] private float energyChaseMultiplier;
    
    private void Awake()
    {
        #region  CONFIGURATION
        
        maxEnergy = energy;
        _rb = GetComponent<Rigidbody>();
        
        var idle = new State<EnemyStates>("IDLE");
        var patrol  = new State<EnemyStates>("PATROL");
        var chase = new State<EnemyStates>("CHASE");
        var attack = new State<EnemyStates>("ATTACK");
        var rest = new State<EnemyStates>("REST");
        
        
        StateConfigurer.Create(idle)
            .SetTransition(EnemyStates.PATROL, patrol)
            .SetTransition(EnemyStates.CHASE, chase)
            .SetTransition(EnemyStates.ATTACK, attack)
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
            .SetTransition(EnemyStates.CHASE, chase)
            .SetTransition(EnemyStates.PATROL, patrol)
            .Done();

        StateConfigurer.Create(rest)
            .SetTransition(EnemyStates.IDLE, idle)
            .Done();
        #endregion

        #region IDLE SETUP
        
        idle.OnEnter += x =>
        {
            if (energy < 0)
            {
                SendInputToFSM(EnemyStates.REST);
                return;
            }
            
            if (Vector3.Distance(transform.position, target.transform.position) <= minChaseDistance &&
                IsInSight(transform.position, target.transform.position))
            {
                SendInputToFSM(EnemyStates.CHASE);
                return;
            }
            
            SendInputToFSM(EnemyStates.PATROL);
        };

        
        #endregion
        
        #region PATROL SETUP

        patrol.OnEnter += x =>
        {
            currentWaypoint = GetClosestPatrolPoint(transform.position);
        };

        patrol.OnUpdate += () =>
        {
            if (Vector3.Distance(transform.position, target.transform.position) <= minChaseDistance &&
                IsInSight(transform.position, target.transform.position))
            {
                SendInputToFSM(EnemyStates.CHASE);
                return;
            }

            Vector3 direction = allWaypoits[currentWaypoint].transform.position - transform.position;
            transform.forward = direction;

            transform.position += transform.forward * patrolSpeed * Time.deltaTime;

            if (Vector3.Distance(transform.position, allWaypoits[currentWaypoint].transform.position) <=
                minPatrolDistance)
            {
                currentWaypoint += waypointSign;

                if (currentWaypoint < 0 || currentWaypoint == allWaypoits.Count)
                {
                    waypointSign *= -1;
                    currentWaypoint += waypointSign;
                }
            }

            energy -= Time.deltaTime * energyPatrolMultiplier;
            
            if (energy <= 0)
            {
                SendInputToFSM(EnemyStates.REST);
                return;
            }
        };

        #endregion
        
        #region CHASE SETUP

        chase.OnUpdate += () =>
        {
            
        };

        #endregion
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

    public int GetClosestPatrolPoint(Vector3 point)
    {
        float minDistance = Mathf.Infinity;
        int current = -1;

        int count = 0;
        foreach (var waypoint in allWaypoits)
        {
            float distance = Vector3.Distance(point, waypoint.transform.position);

            if (distance <= minDistance)
            {
                minDistance = distance;
                current = count;
            }

            count++;
        }

        return current;
    }
    
    private void SendInputToFSM(EnemyStates state)
    {
        _fsm.SendInput(state);
    }

    
}
