using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    public static BoidManager instance;
    public List<GridEntity> allBoids = new List<GridEntity>();

    private void Awake()
    {
        if (instance == null) 
            instance = this;
        else
        {
            Debug.Log("An instance of " + this + " was already present in the scene. Deleting...");
            Destroy(gameObject);
        }
    }

    public void AddBoid(GridEntity boid)
    {
        if (!allBoids.Contains(boid))
            allBoids.Add(boid);
    }

    public void RemoveBoid(GridEntity boid)
    {
        allBoids.Remove(boid);
    }

}
