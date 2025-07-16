using UnityEngine;

[CreateAssetMenu(fileName = "MoveVars", menuName = "Scriptable Objects/MoveVars")]
public class MoveVars : ScriptableObject
{
    public static MoveVars Instance;
    
    public float gravity = 35f;            
    public float stopSpeed = 2.5f;          
    public float maxSpeed = 50f;      
    public float groundAccel = 1f;
    public float airAccel = 0.25f;
    public float slideAccel = 0.75f;
    public float friction = 10;
    public float maxVelocity = 1000f;
    public float moveSpeed = 8f;
    public float jumpVelocity = 10f;
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Instance = Resources.Load<MoveVars>("MoveVars");
    }
}
