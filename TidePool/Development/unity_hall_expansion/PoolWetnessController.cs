using UnityEngine;

public class PoolWetnessController : MonoBehaviour
{
    [SerializeField] private Transform waterFrame;

    [SerializeField]
    private Renderer[] deckRenderers =
        new Renderer[0];

    [SerializeField] private float wetWaterHeight = -0.3f;
    [SerializeField] private float dryWaterHeight = -1.8f;

    [SerializeField] private float wettingSpeed = 0.5f;
    [SerializeField] private float dryingSpeed = 0.1f;

    [SerializeField, Range(0f, 1f)]
    private float currentWetness = 1f;

    private MaterialPropertyBlock propertyBlock;

    private static readonly int WetnessId =
        Shader.PropertyToID("_Wetness");

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (waterFrame == null)
        {
            return;
        }

        // Unity can reload scripts during Play without calling Awake again.
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        float targetWetness = Mathf.InverseLerp(
            dryWaterHeight,
            wetWaterHeight,
            waterFrame.position.y
        );

        float changeSpeed;

        if (targetWetness > currentWetness)
        {
            changeSpeed = wettingSpeed;
        }
        else
        {
            changeSpeed = dryingSpeed;
        }

        currentWetness = Mathf.MoveTowards(
            currentWetness,
            targetWetness,
            changeSpeed * Time.deltaTime
        );

        foreach (Renderer deckRenderer in deckRenderers)
        {
            if (deckRenderer == null)
            {
                continue;
            }

            deckRenderer.GetPropertyBlock(propertyBlock);

            propertyBlock.SetFloat(
                WetnessId,
                currentWetness
            );

            deckRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
