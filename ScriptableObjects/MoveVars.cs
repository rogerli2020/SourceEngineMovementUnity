using UnityEngine;

[CreateAssetMenu(fileName = "MoveVars", menuName = "Scriptable Objects/MoveVars")]
public class MoveVars : ScriptableObject
{
    public static MoveVars Instance;
    
    public float gravity = 35f;            
    public float stopSpeed = 10f;          
    public float maxSpeed = 10f;      
    public float groundAccel = 2f;
    public float airAccel = 1.25f;
    public float slideAccel = 1.4f;
    public float friction = 4.5f;
    public float maxVelocity = 1000f;
    public float moveSpeed = 7.5f;
    public float jumpVelocity = 10f;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Instance = Resources.Load<MoveVars>("MoveVars");
    }
}



// #define PM_GRAVITY 800 
// #define PM_STOPSPEED 100 
// #define PM_MAXSPEED 320 
// #define PM_SPECTATORMAXSPEED 500 
// #define PM_ACCELERATE 10 
// #define PM_AIRACCELERATE 0.7 
// #define PM_WATERACCELERATE 10 
// #define PM_FRICTION 6 
// #define PM_WATERFRICTION 1
