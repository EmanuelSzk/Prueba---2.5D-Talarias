
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpeedHUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private PlayerMove25D player;
    [SerializeField] private Slider speedBar;
    [SerializeField] private TMP_Text speedText;

    private void Start()
    {
        if (speedBar != null)
        {
            speedBar.minValue = 0f;
            speedBar.maxValue = 1f;
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        float reserve = Mathf.Clamp01(player.SpeedReserve);

        // Actualizar el llenado de la barra.
        if (speedBar != null)
            speedBar.value = reserve;

        // Mostrar el estado de la carrera y la reserva restante.
        if (speedText != null)
        {
            int percentage = Mathf.RoundToInt(reserve * 100f);
            int seconds = Mathf.CeilToInt(player.SpeedReserveSeconds);

            if (player.IsFastRunning)
            {
                speedText.text =
                    $"{percentage}%";
            }
            else if (reserve > 0.01f)
            {
                speedText.text =
                    $"{percentage}%";
            }
            else
            {
                speedText.text = $"{percentage}%";
            }
        }
    }
}
