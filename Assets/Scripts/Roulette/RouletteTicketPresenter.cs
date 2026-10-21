using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class RouletteTicketPresenter : MonoBehaviour
{
    [Header("Ticket Authoring")]
    [SerializeField] private RouletteTicketVisual ticketPrefab;
    [SerializeField] private string ticketLayerName = "RouletteTicket";
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform wagerSpawn;
    [SerializeField] private Transform countedPileAnchor;
    [SerializeField] private Transform returnTarget;
    [SerializeField] private TMP_Text wagerCountText;

    [Header("Physics Presentation")]
    [SerializeField, Min(4)] private int liveBodyBudget = 64;
    [SerializeField, Min(0.01f)] private float actionInterval = 0.05f;
    [SerializeField, Min(0.02f)] private float consolidationDwell = 0.2f;
    [SerializeField, Min(0.1f)] private float returnFlightSeconds = 0.6f;
    [SerializeField, Min(0f)] private float spawnRadius = 0.1f;

    [Header("Paper Audio")]
    [SerializeField] private SoundCueSO paperCue;
    [SerializeField] private AudioSource paperSource;
    [SerializeField, Min(0.05f)] private float minimumPaperInterval = 0.1f;

    private struct ReturningTicket
    {
        public RouletteTicketVisual visual;
        public float expires;
    }

    private readonly List<RouletteTicketVisual> singles = new List<RouletteTicketVisual>();
    private readonly List<ReturningTicket> returning = new List<ReturningTicket>();
    private readonly System.Random audioRandom = new System.Random();
    private RouletteTicketVisual bundle;
    private RouletteController owner;
    private int targetCount;
    private int tableCount;
    private float nextActionTime;
    private float nextPaperTime;
    private int previousClip = -1;

    public int RepresentedCount => tableCount;
    public int LiveBodyCount => singles.Count + returning.Count + (bundle != null ? 1 : 0);

    public bool IsReady => isActiveAndEnabled && ticketPrefab != null && visualRoot != null &&
        !ticketPrefab.gameObject.scene.IsValid() &&
        visualRoot.gameObject.activeInHierarchy && wagerSpawn != null && countedPileAnchor != null &&
        returnTarget != null && wagerCountText != null && !transform.IsChildOf(visualRoot) &&
        !wagerSpawn.IsChildOf(visualRoot) && !countedPileAnchor.IsChildOf(visualRoot) &&
        !returnTarget.IsChildOf(visualRoot) && !wagerCountText.transform.IsChildOf(visualRoot) &&
        liveBodyBudget >= 4 && actionInterval > 0f && consolidationDwell > 0f && returnFlightSeconds > 0f &&
        ticketPrefab.HasAuthoredSetup(LayerMask.NameToLayer(ticketLayerName));

    public bool IsPresentingFor(RouletteController controller) => isActiveAndEnabled && owner == controller;

    public bool SyncDraft(RouletteController controller)
    {
        if (controller == null || !controller.UsesTicketPresenter(this) || !controller.IsOpen ||
            !controller.DraftColour.HasValue) return false;
        if (!IsReady)
        {
            CancelDraft(controller);
            return false;
        }
        if (owner != controller)
        {
            ClearOwnedVisuals();
            owner = controller;
        }
        if (targetCount == controller.DraftStake)
        {
            RefreshCountText();
            return true;
        }
        targetCount = controller.DraftStake;
        ReconcileOne();
        RefreshCountText();
        return true;
    }

    public void CancelDraft(RouletteController controller)
    {
        if (owner == controller) ClearOwnedVisuals();
    }

    private void Update()
    {
        if (owner == null)
        {
            if (LiveBodyCount > 0 || targetCount > 0) ClearOwnedVisuals();
            return;
        }
        if (!owner.isActiveAndEnabled || !owner.IsOpen || !owner.DraftColour.HasValue)
        {
            ClearOwnedVisuals();
            return;
        }
        for (int i = returning.Count - 1; i >= 0; i--)
        {
            if (returning[i].visual != null && Time.time < returning[i].expires) continue;
            Retire(returning[i].visual);
            returning.RemoveAt(i);
        }
        for (int i = singles.Count - 1; i >= 0; i--)
        {
            if (singles[i] != null) continue;
            singles.RemoveAt(i);
            tableCount--;
        }
        if (bundle == null) tableCount = singles.Count;
        if (visualRoot == null || !visualRoot.gameObject.activeInHierarchy || wagerSpawn == null ||
            countedPileAnchor == null || returnTarget == null || ticketPrefab == null)
        {
            ClearOwnedVisuals();
            return;
        }
        ReconcileOne();
        RefreshCountText();
    }

    private void ReconcileOne()
    {
        if (Time.time < nextActionTime || targetCount == tableCount) return;
        bool changed = targetCount > tableCount ? DropOne() : ReturnOne();
        if (changed) nextActionTime = Time.time + actionInterval;
    }

    private bool MakeRoom()
    {
        // Leave room for one counted bundle
        while (LiveBodyCount >= liveBodyBudget - (bundle == null ? 1 : 0))
            if (!ConsolidateOne()) return false;
        return true;
    }

    private bool ConsolidateOne()
    {
        for (int i = 0; i < singles.Count; i++)
        {
            RouletteTicketVisual ticket = singles[i];
            if (ticket == null || !ticket.CanConsolidate(consolidationDwell)) continue;
            if (bundle == null)
            {
                bundle = Instantiate(ticketPrefab, countedPileAnchor.position, countedPileAnchor.rotation, visualRoot);
                bundle.Body.linearVelocity = Vector3.zero;
                bundle.Body.angularVelocity = Vector3.zero;
                bundle.Body.isKinematic = true;
                bundle.Body.useGravity = false;
                bundle.SetUnits(0);
            }
            bundle.SetUnits(checked(bundle.Units + 1));
            Retire(ticket);
            singles.RemoveAt(i);
            return true;
        }
        return false;
    }

    private bool DropOne()
    {
        if (!MakeRoom()) return false;
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = wagerSpawn.TransformPoint(new Vector3(offset.x, 0f, offset.y));
        RouletteTicketVisual ticket = Instantiate(ticketPrefab, position, wagerSpawn.rotation, visualRoot);
        ticket.SetUnits(1);
        singles.Add(ticket);
        tableCount++;
        PlayPaper(position);
        return true;
    }

    private bool ReturnOne()
    {
        RouletteTicketVisual ticket;
        if (singles.Count > 0)
        {
            int last = singles.Count - 1;
            ticket = singles[last];
            singles.RemoveAt(last);
            if (ticket == null) { tableCount--; return true; }
        }
        else if (bundle != null && bundle.Units > 0)
        {
            if (!MakeRoom()) return false;
            ticket = Instantiate(ticketPrefab, countedPileAnchor.position, countedPileAnchor.rotation, visualRoot);
            bundle.SetUnits(bundle.Units - 1);
            if (bundle.Units == 0) { Retire(bundle); bundle = null; }
        }
        else return false;
        tableCount--;
        ticket.SetUnits(1);
        Rigidbody body = ticket.Body;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = (returnTarget.position - body.position) / returnFlightSeconds -
            0.5f * Physics.gravity * returnFlightSeconds;
        body.WakeUp();
        returning.Add(new ReturningTicket { visual = ticket, expires = Time.time + returnFlightSeconds + 0.2f });
        PlayPaper(body.position);
        return true;
    }

    private void RefreshCountText()
    {
        if (wagerCountText == null) return;
        wagerCountText.raycastTarget = false;
        wagerCountText.text = owner == null ? "" : $"Wager: {targetCount}\nOn table: {tableCount}" +
            (targetCount > tableCount ? $"\nArriving: {targetCount - tableCount}" :
                targetCount < tableCount ? $"\nTo return: {tableCount - targetCount}" : "");
    }

    private void PlayPaper(Vector3 position)
    {
        if (paperCue == null || paperCue.output == null || paperSource == null || !paperSource.isActiveAndEnabled ||
            paperSource.transform == transform || !paperSource.transform.IsChildOf(transform) ||
            AudioListener.pause || paperSource.isPlaying || Time.unscaledTime < nextPaperTime) return;
        AudioClip clip = paperCue.PickVariant(audioRandom, previousClip, out int selected);
        if (clip == null || clip.loadState != AudioDataLoadState.Loaded) return;
        previousClip = selected;
        paperSource.transform.position = position;
        paperSource.playOnAwake = false;
        paperSource.loop = false;
        paperSource.ignoreListenerPause = false;
        paperSource.clip = clip;
        paperSource.outputAudioMixerGroup = paperCue.output;
        paperSource.volume = Mathf.Clamp01(paperCue.gain);
        paperSource.pitch = Mathf.Clamp(Random.Range(paperCue.pitchRange.x, paperCue.pitchRange.y), 0.1f, 3f);
        paperSource.priority = Mathf.Clamp(paperCue.priority, 0, 256);
        paperSource.spatialBlend = Mathf.Clamp01(paperCue.spatialBlend);
        paperSource.dopplerLevel = 0f;
        paperSource.rolloffMode = AudioRolloffMode.Logarithmic;
        paperSource.minDistance = Mathf.Max(0.01f, paperCue.minDistance);
        paperSource.maxDistance = Mathf.Max(paperSource.minDistance, paperCue.maxDistance);
        paperSource.Play();
        nextPaperTime = Time.unscaledTime + Mathf.Max(minimumPaperInterval, paperCue.cooldown);
    }

    private void ClearOwnedVisuals()
    {
        foreach (RouletteTicketVisual ticket in singles) Retire(ticket);
        foreach (ReturningTicket ticket in returning) Retire(ticket.visual);
        Retire(bundle);
        singles.Clear();
        returning.Clear();
        bundle = null;
        owner = null;
        targetCount = 0;
        tableCount = 0;
        nextActionTime = 0f;
        nextPaperTime = 0f;
        if (paperSource != null && paperSource.transform != transform && paperSource.transform.IsChildOf(transform))
        {
            paperSource.Stop();
            paperSource.clip = null;
        }
        RefreshCountText();
    }

    private static void Retire(RouletteTicketVisual ticket)
    {
        if (ticket == null) return;
        ticket.gameObject.SetActive(false);
        Destroy(ticket.gameObject);
    }

    private void Awake()
    {
        if (paperSource != null && paperSource.transform != transform && paperSource.transform.IsChildOf(transform))
        {
            paperSource.playOnAwake = false;
            paperSource.loop = false;
        }
        ClearOwnedVisuals();
    }

    private void OnDisable() => ClearOwnedVisuals();
    private void OnDestroy() => ClearOwnedVisuals();
}
