using UnityEngine;

[CreateAssetMenu(fileName = "MoveVars", menuName = "Scriptable Objects/MoveVars")]
public class MoveVars : ScriptableObject
{
    public static MoveVars Instance;
    
    public float gravity = 35f;            
    public float stopSpeed = 2.5f;          
    public float maxSpeed = 10f;      
    public float groundAccel = 1f;
    public float airAccel = 0.6f;
    public float slideAccel = 0.75f;
    public float friction = 7.5f;
    public float maxVelocity = 1000f;
    public float moveSpeed = 7.5f;
    public float jumpVelocity = 10f;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Instance = Resources.Load<MoveVars>("MoveVars");
    }
}
