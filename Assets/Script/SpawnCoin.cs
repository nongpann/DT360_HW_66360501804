using UnityEngine;

public class SpawnCoin : MonoBehaviour
{
    [SerializeField] private GameObject coin;
    private float currtime = 0;
    private float respawnTime = 0.25f;
    public int numberCoin = 0;
    [SerializeField] private StateMachine isIdle;
    public Transform parentObj;
    void Update()
    {
        currtime += Time.deltaTime;

        if (currtime > respawnTime && numberCoin < 5 && isIdle.getState() == "Idle")
        {
            currtime = 0;
            numberCoin += 1;
            float randomX = Random.Range(-9.0f, 9.0f);
            float randomZ = Random.Range(-9.0f, 9.0f);
            GameObject coinObj = Instantiate(coin, new Vector3(randomX, 1, randomZ), Quaternion.identity);
            coinObj.transform.SetParent(parentObj, false);
        }
        
    }
}
