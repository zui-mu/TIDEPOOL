using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class BathhouseEntrance : MonoBehaviour
{
    const string Destination = "Assets/_Game/Scenes/Bathhouse_02.unity";

    [SerializeField] private CharacterController player;
    [SerializeField] private PoolWaterCycle waterCycle;

    private AsyncOperation loading;

    private void OnTriggerEnter(Collider other)
    {
        if (!isActiveAndEnabled || loading != null || other != player || !waterCycle)
            return;

        if (waterCycle.CurrentPhase != PoolWaterCycle.Phase.Filling ||
            !waterCycle.letter || !waterCycle.letter.PickedUp)
            return;

        int sceneIndex = SceneUtility.GetBuildIndexByScenePath(Destination);
        if (sceneIndex < 0)
        {
            Debug.LogError($"Add {Destination} to the active build scene list to open the bathhouse.", this);
            return;
        }

        loading = SceneManager.LoadSceneAsync(sceneIndex, LoadSceneMode.Single);
    }
}
