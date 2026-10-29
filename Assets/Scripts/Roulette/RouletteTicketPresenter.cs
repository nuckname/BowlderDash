using System.Collections.Generic;
using TMPro;
using UnityEngine;

public sealed class RouletteTicketPresenter : MonoBehaviour
{
    private enum ResultPhase { Draft, Win, Loss, Complete }

    [Header("Ticket Authoring")]
    [SerializeField] private RouletteTicketVisual ticketPrefab;
    [SerializeField] private string ticketLayerName = "RouletteTicket";
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform wagerSpawn;
    [SerializeField] private Transform wagerPileAnchor;
    [SerializeField] private Transform returnPileAnchor;
    [SerializeField] private Transform returnTarget;
    [SerializeField] private TMP_Text wagerCountText;

    [Header("Physics Presentation")]
    [SerializeField, Min(4)] private int liveBodyBudget = 64;
    [SerializeField, Min(0.01f)] private float actionInterval = 0.05f;
    [SerializeField, Min(0.02f)] private float consolidationDwell = 0.2f;
    [SerializeField, Min(0.1f)] private float returnFlightSeconds = 0.6f;
    [SerializeField, Min(0f)] private float spawnRadius = 0.1f;

    [Header("Reward Presentation")]
    [SerializeField] private Transform rewardSpawn;
    [SerializeField] private TMP_Text resultProgressText;
    [SerializeField, Min(0.02f), Tooltip("Minimum seconds between visible reward drops")]
    private float rewardInterval = 0.06f;
    [SerializeField, Min(0.1f), Tooltip("Longest reward stream before the remaining units join the return pile")]
    private float maximumRewardSeconds = 5f;

    [Header("Loss Presentation")]
    [SerializeField] private ParticleSystem lossSmokePrefab;
    [SerializeField] private Transform smokeSpawn;
    [SerializeField, Min(0f), Tooltip("Seconds after the puff begins before stake visuals disappear")]
    private float lossRetireDelay = 0.18f;
    [SerializeField, Min(0.1f)] private float lossPresentationSeconds = 2f;
    [SerializeField] private SoundCueSO smokeCue;

    [Header("Paper Audio")]
    [SerializeField] private SoundCueSO paperCue;
    [SerializeField] private AudioSource paperSource;
    [SerializeField, Min(0.05f)] private float minimumPaperInterval = 0.1f;

    private struct ReturningTicket
    {
        public RouletteTicketVisual visual;
        public float started;
        public float expires;
        public Vector3 start;
        public Vector3 destination;
    }

    private readonly List<RouletteTicketVisual> singles = new List<RouletteTicketVisual>();
    private readonly List<ReturningTicket> returning = new List<ReturningTicket>();
    private readonly System.Random audioRandom = new System.Random();
    private readonly HashSet<RouletteTicketVisual> rewardTickets = new HashSet<RouletteTicketVisual>();
    private RouletteTicketVisual bundle;
    private RouletteTicketVisual rewardBundle;
    private RouletteController owner;
    private int targetCount;
    private int tableCount;
    private float nextActionTime;
    private float nextPaperTime;
    private int previousClip = -1;
    private ResultPhase resultPhase;
    private int resultSpinId;
    private int stakeUnits;
    private int totalReturn;
    private int profitUnits;
    private int shownProfit;
    private float resultAge;
    private float rewardDuration;
    private float nextRewardAge;
    private bool lossRetired;
    private bool audioSuppressed;
    private ParticleSystem smoke;
    private GameAudio audioService;

    public int RepresentedCount => tableCount;
    public int LiveBodyCount => singles.Count + returning.Count + (bundle != null ? 1 : 0) + (rewardBundle != null ? 1 : 0);

    public bool IsReady => isActiveAndEnabled && ticketPrefab != null && visualRoot != null &&
        !ticketPrefab.gameObject.scene.IsValid() &&
        visualRoot.gameObject.activeInHierarchy && wagerSpawn != null && wagerPileAnchor != null && returnPileAnchor != null &&
        returnTarget != null && wagerCountText != null && !transform.IsChildOf(visualRoot) &&
        !wagerSpawn.IsChildOf(visualRoot) && !wagerPileAnchor.IsChildOf(visualRoot) &&
        !returnPileAnchor.IsChildOf(visualRoot) &&
        !returnTarget.IsChildOf(visualRoot) && !wagerCountText.transform.IsChildOf(visualRoot) &&
        liveBodyBudget >= 4 && Positive(actionInterval) && Positive(consolidationDwell) &&
        Positive(returnFlightSeconds) && NonNegative(spawnRadius) && Positive(minimumPaperInterval) &&
        ticketPrefab.HasAuthoredSetup(LayerMask.NameToLayer(ticketLayerName)) && HasResultSetup;

    private bool HasResultSetup => rewardSpawn != null && rewardSpawn.gameObject.activeInHierarchy &&
        smokeSpawn != null && smokeSpawn.gameObject.activeInHierarchy &&
        !rewardSpawn.IsChildOf(visualRoot) && !smokeSpawn.IsChildOf(visualRoot) &&
        resultProgressText != null && resultProgressText.isActiveAndEnabled &&
        !resultProgressText.transform.IsChildOf(visualRoot) &&
        Positive(rewardInterval) && Positive(maximumRewardSeconds) && maximumRewardSeconds >= rewardInterval &&
        Positive(lossPresentationSeconds) && NonNegative(lossRetireDelay) && lossRetireDelay < lossPresentationSeconds &&
        lossSmokePrefab != null && !lossSmokePrefab.gameObject.scene.IsValid() &&
        lossSmokePrefab.gameObject.activeSelf && !lossSmokePrefab.main.loop && !lossSmokePrefab.main.playOnAwake &&
        lossSmokePrefab.main.maxParticles > 0 && lossSmokePrefab.main.maxParticles <= 128 &&
        lossSmokePrefab.GetComponentsInChildren<ParticleSystem>(true).Length == 1 &&
        lossSmokePrefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 &&
        lossSmokePrefab.GetComponentsInChildren<Collider>(true).Length == 0 &&
        lossSmokePrefab.GetComponentsInChildren<Rigidbody>(true).Length == 0 &&
        lossSmokePrefab.GetComponentsInChildren<AudioSource>(true).Length == 0;

    public bool IsPresentingFor(RouletteController controller) => isActiveAndEnabled && owner == controller;

    public bool IsResultPlayingFor(RouletteController controller, int spinId) =>
        IsPresentingFor(controller) && resultSpinId == spinId &&
        (resultPhase == ResultPhase.Win || resultPhase == ResultPhase.Loss);

    public bool SyncDraft(RouletteController controller)
    {
        if (controller == null || !controller.UsesTicketPresenter(this) || !controller.IsOpen ||
            !controller.DraftColour.HasValue || resultPhase != ResultPhase.Draft) return false;
        if (!IsReady) { CancelDraft(controller); return false; }
        if (owner != controller)
        {
            ClearOwnedVisuals();
            owner = controller;
        }
        targetCount = controller.DraftStake;
        ReconcileOne();
        RefreshCountText();
        return true;
    }

    public bool BeginResult(RouletteController controller, int spinId, int stake, int settledReturn)
    {
        if (!IsReady || !IsPresentingFor(controller) || resultPhase != ResultPhase.Draft || stake <= 0 ||
            (settledReturn != 0 && settledReturn <= stake) ||
            !controller.IsPresentingResultFor(this, spinId)) return false;
        resultSpinId = spinId;
        stakeUnits = stake;
        totalReturn = settledReturn;
        profitUnits = settledReturn > 0 ? settledReturn - stake : 0;
        ClearReturns();
        PruneVisuals();
        SetPileCount(stake);
        targetCount = settledReturn;
        resultAge = 0f;
        shownProfit = 0;
        lossRetired = false;
        if (settledReturn > 0)
        {
            resultPhase = ResultPhase.Win;
            rewardDuration = (float)System.Math.Min(maximumRewardSeconds,
                System.Math.Max(rewardInterval, (double)profitUnits * rewardInterval));
            nextRewardAge = rewardInterval;
        }
        else
        {
            resultPhase = ResultPhase.Loss;
            smoke = Instantiate(lossSmokePrefab, smokeSpawn.position, smokeSpawn.rotation);
            smoke.transform.SetParent(visualRoot, true);
            smoke.Play(true);
            if (!audioSuppressed && smokeCue != null) audioService?.PlayOwned(smokeCue, this, smokeSpawn.position);
        }
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
            if (LiveBodyCount > 0 || smoke != null || targetCount > 0) ClearOwnedVisuals();
            return;
        }
        if (!owner.isActiveAndEnabled || !owner.IsOpen || !owner.DraftColour.HasValue ||
            visualRoot == null || !visualRoot.gameObject.activeInHierarchy || wagerSpawn == null ||
            wagerPileAnchor == null || returnPileAnchor == null || returnTarget == null || ticketPrefab == null)
        {
            ClearOwnedVisuals();
            return;
        }
        PruneVisuals();
        if (resultPhase == ResultPhase.Draft) ReconcileOne();
        else
        {
            if (!owner.IsPresentingResultFor(this, resultSpinId)) { ClearOwnedVisuals(); return; }
            if (resultPhase == ResultPhase.Win || resultPhase == ResultPhase.Loss)
            {
                if (rewardSpawn == null || smokeSpawn == null) { ClearOwnedVisuals(); return; }
                resultAge += Time.deltaTime;
                if (resultPhase == ResultPhase.Win) UpdateRewards();
                else UpdateLoss();
            }
            else if (totalReturn > 0) SetPileCount(totalReturn);
        }
        RefreshCountText();
    }

    private void PruneVisuals()
    {
        for (int i = returning.Count - 1; i >= 0; i--)
        {
            ReturningTicket ticket = returning[i];
            if (ticket.visual == null || Time.time >= ticket.expires)
            {
                Retire(ticket.visual);
                returning.RemoveAt(i);
                continue;
            }
            Rigidbody body = ticket.visual.Body;
            if (!body.isKinematic) continue;
            float progress = Mathf.Clamp01((Time.time - ticket.started) / returnFlightSeconds);
            body.position = Vector3.Lerp(ticket.start, ticket.destination, progress) +
                Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.8f);
            if (progress < 1f) continue;
            body.detectCollisions = true;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }
        tableCount = checked((bundle != null ? bundle.Units : 0) + (rewardBundle != null ? rewardBundle.Units : 0));
        for (int i = singles.Count - 1; i >= 0; i--)
        {
            if (singles[i] == null) { rewardTickets.Remove(singles[i]); singles.RemoveAt(i); continue; }
            tableCount = checked(tableCount + singles[i].Units);
        }
    }

    private void UpdateRewards()
    {
        SetPileCount(checked(stakeUnits + shownProfit));
        if (resultAge >= rewardDuration)
        {
            // Keep remaining winnings in Return when paper has not settled in time
            int remaining = profitUnits - shownProfit;
            if (remaining > 0 && !DropUnits(remaining, rewardSpawn)) AddToBundle(remaining, true);
            shownProfit = profitUnits;
            resultPhase = ResultPhase.Complete;
            return;
        }
        if (resultAge < nextRewardAge) return;
        nextRewardAge = resultAge + rewardInterval;
        int due = (int)System.Math.Min(profitUnits,
            System.Math.Floor((double)profitUnits * resultAge / rewardDuration));
        int units = due - shownProfit;
        if (units > 0 && DropUnits(units, rewardSpawn)) shownProfit += units;
    }

    private void UpdateLoss()
    {
        if (!lossRetired && resultAge >= lossRetireDelay)
        {
            ClearPile();
            lossRetired = true;
        }
        if (resultAge < lossPresentationSeconds) return;
        ClearPile();
        ClearSmoke();
        resultPhase = ResultPhase.Complete;
    }

    private void SetPileCount(int units)
    {
        if (tableCount < units)
        {
            int missing = units - tableCount;
            if (resultPhase == ResultPhase.Draft || totalReturn == 0) AddToBundle(missing);
            else
            {
                int rewardCount = rewardBundle != null ? rewardBundle.Units : 0;
                foreach (RouletteTicketVisual ticket in singles)
                    if (rewardTickets.Contains(ticket)) rewardCount = checked(rewardCount + ticket.Units);
                int missingStake = Mathf.Clamp(stakeUnits - (tableCount - rewardCount), 0, missing);
                AddToBundle(missingStake);
                AddToBundle(missing - missingStake, true);
            }
            return;
        }
        int excess = tableCount - units;
        for (int i = singles.Count - 1; i >= 0 && excess > 0; i--)
        {
            var ticket = singles[i];
            int removed = Mathf.Min(excess, ticket.Units);
            ticket.SetUnits(ticket.Units - removed);
            excess -= removed;
            tableCount -= removed;
            if (ticket.Units == 0) { rewardTickets.Remove(ticket); Retire(ticket); singles.RemoveAt(i); }
        }
        if (excess > 0 && rewardBundle != null)
        {
            int removed = Mathf.Min(excess, rewardBundle.Units);
            rewardBundle.SetUnits(rewardBundle.Units - removed);
            excess -= removed;
            tableCount -= removed;
            if (rewardBundle.Units == 0) { Retire(rewardBundle); rewardBundle = null; }
        }
        if (excess > 0 && bundle != null)
        {
            bundle.SetUnits(bundle.Units - excess);
            tableCount -= excess;
            if (bundle.Units == 0) { Retire(bundle); bundle = null; }
        }
    }

    private void ReconcileOne()
    {
        if (Time.time < nextActionTime || targetCount == tableCount) return;
        bool changed = targetCount > tableCount ? DropUnits(1, wagerSpawn) : ReturnOne();
        if (changed) nextActionTime = Time.time + actionInterval;
    }

    private bool MakeRoom()
    {
        // Reserve room to group tickets within each remaining pile
        while (LiveBodyCount >= liveBodyBudget - 2)
            if (!ConsolidateOne()) return false;
        return true;
    }

    private bool ConsolidateOne()
    {
        for (int i = 0; i < singles.Count; i++)
        {
            RouletteTicketVisual ticket = singles[i];
            if (ticket == null || !ticket.CanConsolidate(consolidationDwell)) continue;
            RouletteTicketVisual destination = EnsureBundle(rewardTickets.Remove(ticket));
            destination.SetUnits(checked(destination.Units + ticket.Units));
            Retire(ticket);
            singles.RemoveAt(i);
            return true;
        }
        return false;
    }

    private RouletteTicketVisual EnsureBundle(bool reward)
    {
        RouletteTicketVisual existing = reward ? rewardBundle : bundle;
        if (existing != null) return existing;
        Transform anchor = reward ? returnPileAnchor : wagerPileAnchor;
        RouletteTicketVisual created = Instantiate(ticketPrefab, anchor.position, anchor.rotation, visualRoot);
        created.Body.linearVelocity = Vector3.zero;
        created.Body.angularVelocity = Vector3.zero;
        created.Body.isKinematic = true;
        created.Body.useGravity = false;
        created.SetUnits(0);
        if (reward) rewardBundle = created;
        else bundle = created;
        return created;
    }

    private void AddToBundle(int units, bool reward = false)
    {
        if (units <= 0) return;
        RouletteTicketVisual destination = EnsureBundle(reward);
        destination.SetUnits(checked(destination.Units + units));
        tableCount = checked(tableCount + units);
    }

    private bool DropUnits(int units, Transform spawn)
    {
        if (!MakeRoom()) return false;
        Vector2 offset = Random.insideUnitCircle * spawnRadius;
        Vector3 position = spawn.TransformPoint(new Vector3(offset.x, 0f, offset.y));
        RouletteTicketVisual ticket = Instantiate(ticketPrefab, position, spawn.rotation, visualRoot);
        ticket.SetUnits(units);
        singles.Add(ticket);
        if (spawn == rewardSpawn) rewardTickets.Add(ticket);
        tableCount = checked(tableCount + units);
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
            if (ticket == null) { singles.RemoveAt(last); return false; }
            singles.RemoveAt(last);
            rewardTickets.Remove(ticket);
        }
        else if (bundle != null && bundle.Units > 0)
        {
            if (!MakeRoom()) return false;
            ticket = Instantiate(ticketPrefab, wagerPileAnchor.position, wagerPileAnchor.rotation, visualRoot);
            bundle.SetUnits(bundle.Units - 1);
            if (bundle.Units == 0) { Retire(bundle); bundle = null; }
        }
        else return false;
        tableCount--;
        ticket.SetUnits(1);
        Rigidbody body = ticket.Body;
        // Clear the wager pile before restoring physical contact at Return
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = false;
        returning.Add(new ReturningTicket
        {
            visual = ticket,
            started = Time.time,
            expires = Time.time + returnFlightSeconds + 0.2f,
            start = body.position,
            destination = returnTarget.position
        });
        PlayPaper(body.position);
        return true;
    }

    private void RefreshCountText()
    {
        if (wagerCountText != null)
        {
            wagerCountText.raycastTarget = false;
            wagerCountText.text = owner == null ? "" : resultPhase == ResultPhase.Draft ?
                $"Wager: {targetCount}\nOn table: {tableCount}" +
                (targetCount > tableCount ? $"\nArriving: {targetCount - tableCount}" :
                    targetCount < tableCount ? $"\nTo return: {tableCount - targetCount}" : "") :
                $"Stake: {stakeUnits}\nOn table: {tableCount}";
        }
        if (resultProgressText != null)
        {
            resultProgressText.raycastTarget = false;
            resultProgressText.text = owner == null || resultPhase == ResultPhase.Draft ? "" :
                totalReturn > 0 ? $"Total return: {totalReturn}\nProfit added: {shownProfit} / {profitUnits}" :
                $"Total return: 0\nLost stake: {stakeUnits}";
        }
    }

    private void PlayPaper(Vector3 position)
    {
        if (audioSuppressed || paperCue == null || paperCue.output == null || paperSource == null || !paperSource.isActiveAndEnabled ||
            AudioListener.pause || paperSource.isPlaying || Time.unscaledTime < nextPaperTime ||
            paperSource.transform == transform || !paperSource.transform.IsChildOf(transform)) return;
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

    private void ClearReturns()
    {
        foreach (ReturningTicket ticket in returning) Retire(ticket.visual);
        returning.Clear();
    }

    private void ClearPile()
    {
        foreach (RouletteTicketVisual ticket in singles) Retire(ticket);
        Retire(bundle);
        Retire(rewardBundle);
        singles.Clear();
        rewardTickets.Clear();
        bundle = rewardBundle = null;
        tableCount = 0;
    }

    private void ClearSmoke()
    {
        if (smoke != null) { smoke.gameObject.SetActive(false); Destroy(smoke.gameObject); }
        smoke = null;
    }

    private void StopPresentationAudio()
    {
        audioSuppressed = true;
        if (paperSource != null && paperSource.transform != transform && paperSource.transform.IsChildOf(transform))
        {
            paperSource.Stop();
            paperSource.clip = null;
        }
        if (audioService != null) audioService.StopOwnedTransients(this);
    }

    private void ClearOwnedVisuals()
    {
        ClearReturns();
        ClearPile();
        ClearSmoke();
        StopPresentationAudio();
        owner = null;
        targetCount = 0;
        nextActionTime = nextPaperTime = resultAge = 0f;
        resultPhase = ResultPhase.Draft;
        resultSpinId = stakeUnits = totalReturn = profitUnits = shownProfit = 0;
        audioSuppressed = false;
        RefreshCountText();
    }

    private static void Retire(RouletteTicketVisual ticket)
    {
        if (ticket == null) return;
        ticket.gameObject.SetActive(false);
        Destroy(ticket.gameObject);
    }

    private void Awake() => ClearOwnedVisuals();

    private void OnEnable()
    {
        audioService = GameAudio.Instance;
        if (audioService != null) audioService.WorldAudioStopped += StopPresentationAudio;
    }

    private void OnDisable()
    {
        ClearOwnedVisuals();
        if (audioService != null) audioService.WorldAudioStopped -= StopPresentationAudio;
        audioService = null;
    }

    private void OnDestroy() => ClearOwnedVisuals();
    private static bool Positive(float value) => RouletteWheelMotor.IsFinite(value) && value > 0f;
    private static bool NonNegative(float value) => RouletteWheelMotor.IsFinite(value) && value >= 0f;
}
