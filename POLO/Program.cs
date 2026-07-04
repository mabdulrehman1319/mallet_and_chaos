using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  HORSE HOCKEY  -  2 Teams x 4 Players  -  .NET Framework 4.8
//
//  CONTROLS
//  Tab          = Switch active player
//  Arrow Keys   = Move active player
//  Space        = Hit ball (swing stick)
//  P            = Pause / Resume
//  Esc          = Back to menu (from pause)
//  R            = Restart (game-over screen)
// ======================================================================
namespace HockeyHorseGame
{
    // ─────────────────────────── ENTRY POINT ───────────────────────────
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }
    }

    // ─────────────────────────── ENUMS ─────────────────────────────────
    enum Team { Blue, Red }
    enum Role { Defender, Scorer, Captain, Helper }
    enum Difficulty { Low, Medium, Hard }
    enum GameState { Menu, Playing, Paused, RoundOver, GameOver, Practice }

    // ─────────────────────────── PLAYER CLASS ──────────────────────────
    class Player
    {
        public float X, Y, VX, VY;
        public Team Side;
        public Role Role;
        public Color BodyColor, JerseyColor, HelmetColor;
        public string Name;
        public bool IsHitting;
        public int HitTimer;
        public float TargetX, TargetY;
        public int AIThinkTimer;
        public bool IsActive;      // currently controlled by human
        public int Number;        // jersey number 1-4
        public int GoalsThisMatch = 0;
        public int HitsThisMatch = 0;
    }

    // ─────────────────────────── BALL CLASS ────────────────────────────
    class Ball
    {
        public float X, Y, VX, VY;
        public const float R = 9f;
        public const float Friction = 0.984f;
    }

    // ─────────────────────────── PARTICLE ──────────────────────────────
    class Particle
    {
        public float X, Y, VX, VY, Size, Alpha;
        public Color Col;
        public bool Dead { get { return Alpha <= 0f; } }

        public Particle(float x, float y, Color c, Random rng, float spd)
        {
            X = x; Y = y; Col = c;
            float a = (float)(rng.NextDouble() * Math.PI * 2.0);
            float s = (float)(rng.NextDouble() * spd + 0.5f);
            VX = (float)Math.Cos(a) * s;
            VY = (float)Math.Sin(a) * s;
            Size = (float)(rng.NextDouble() * 5.0 + 2.0);
            Alpha = 1f;
        }
        public void Update()
        {
            X += VX; Y += VY;
            VX *= 0.93f; VY *= 0.93f; VY += 0.12f;
            Alpha -= 0.03f; Size *= 0.97f;
        }
    }


    public class GameForm : Form
    {

        const int W = 1000, H = 640;
        const int FL = 70, FR = 930;   
        const int FT = 90, FB = 550;  
        const int FW = FR - FL, FH = FB - FT;
        const int GT = 245, GB = 395;   
        const int GD = 45;               

        int currentUserId = -1;
        int currentMatchId = -1;
        int currentRoundId = -1;
        bool goalJustScored = false;   // prevents multi-counting goals per tick

        Player lastHitPlayer = null;

        // ── State ───────────────────────────────────────────────────────
        GameState state = GameState.Menu;
        Difficulty aiLevel = Difficulty.Medium;
        int maxRounds = 3;
        int currentRound = 0;
        int roundSeconds = 120;  // 2 minutes
        int timeLeft;
        int tickAccum = 0;
        int blueRoundWins = 0, redRoundWins = 0;
        int blueGoals = 0, redGoals = 0;
        string roundResultMsg = "";
        int roundResultTimer = 0;

        // ── Players ─────────────────────────────────────────────────────
        List<Player> bluePlayers = new List<Player>();
        List<Player> redPlayers = new List<Player>();
        int activeBlueIndex = 0;  // which blue player human controls

        // ── Ball ────────────────────────────────────────────────────────
        Ball ball = new Ball();

        // ── Input ───────────────────────────────────────────────────────
        bool keyLeft, keyRight, keyUp, keyDown, keyHit;

        // ── Misc ────────────────────────────────────────────────────────
        Timer gTimer = new Timer();
        Random rng = new Random();
        List<Particle> parts = new List<Particle>();
        int menuSel = 1; // 0=Low 1=Medium 2=Hard

        // ── Practice Mode ─────────────────────────────────────────────
        // menuMode: 0=difficulty rows, 1=mode selection (Practice/Play)
        int menuMode = 0;
        bool practiceActive = false;

        // Practice steps: 0-3 = one step per player role
        // Each step shows a guide overlay and lets user practise that role
        int practiceStep = 0;   // 0=Defender 1=Scorer 2=Captain 3=Helper
        int practiceTimer = 0;   // countdown for each step (set when step starts)
        bool practiceWaiting = true; // waiting for user to press SPACE to begin step
        int practiceArrowAnim = 0; // flashing arrow animation counter

        // Per-step task tracking
        bool taskDone = false;  // user completed the step objective
        int taskDoneTimer = 0;

        // ── AI tuning per difficulty ─────────────────────────────────────
        float aiSpeed, aiHitRange, aiReactTicks;

        // ── Stadium crowd ─────────────────────────────────────────────────
        struct Fan
        {
            public float X, Y, BobPhase, BobSpeed;
            public int ColorIdx;
            public bool FlashOn;
            public int FlashTimer;
            public int FlashInterval;
            public float FlashAngle;
        }
        Fan[] fans;
        int crowdExcite = 0;
        Color[] fanPalette;



        // ================================================================
        public GameForm()
        {
            Text = "Horse Hockey  -  2 vs 2... 4 vs 4!";
            ClientSize = new Size(W, H);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(15, 18, 30);

            gTimer.Interval = 16;
            gTimer.Tick += OnTick;
            gTimer.Start();

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;

            AskForUsername();
        }
        void AskForUsername()
        {
            string username = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter your username to save your stats:",
                "POLO Game", "Player1");

            if (string.IsNullOrWhiteSpace(username))
                username = "Player1";

            currentUserId = DatabaseHelper.GetOrCreateUser(username);
        }

        // ================================================================
        //  TICK
        // ================================================================
        void OnTick(object sender, EventArgs e)
        {
            if (state == GameState.Playing)
            {
                GameUpdate();
                tickAccum++;
                if (tickAccum >= 60)
                {
                    tickAccum = 0;
                    timeLeft--;
                    if (timeLeft <= 0) EndRound();
                }
            }
            if (state == GameState.Practice)
            {
                UpdatePractice();
            }
            if (roundResultTimer > 0) roundResultTimer--;
            practiceArrowAnim++;
            Invalidate();
        }

        // ================================================================
        //  INIT
        // ================================================================
        void SetAITuning()
        {
            switch (aiLevel)
            {
                case Difficulty.Low:
                    aiSpeed = 1.8f; aiHitRange = 38f; aiReactTicks = 22f; break;
                case Difficulty.Medium:
                    aiSpeed = 2.6f; aiHitRange = 46f; aiReactTicks = 14f; break;
                case Difficulty.Hard:
                    aiSpeed = 3.4f; aiHitRange = 55f; aiReactTicks = 7f; break;
            }
        }

        // ================================================================
        //  PRACTICE MODE
        // ================================================================
        void StartPractice()
        {
            SetAITuning();
            BuildCrowd();
            currentRound = 0;
            blueRoundWins = 0;
            redRoundWins = 0;
            blueGoals = 0;
            redGoals = 0;
            BuildTeams();
            PlacePlayersKickoff();
            ball.X = W / 2f; ball.Y = (FT + FB) / 2f;
            ball.VX = 0f; ball.VY = 0f;

            practiceStep = 0;
            practiceWaiting = true;
            taskDone = false;
            taskDoneTimer = 0;

            // Force active player to match step 0 = Defender
            activeBlueIndex = 0;
            MarkActivePlayer();

            practiceActive = true;
            state = GameState.Practice;
        }

        void UpdatePractice()
        {
            if (practiceWaiting) return;   // waiting for SPACE to start step

            // Run normal game physics in practice
            HandlePlayerInput();
            for (int i = 0; i < 4; i++)
                if (i != activeBlueIndex) RunAI(bluePlayers[i], Team.Blue);
            // No red AI opponents in practice — give user a clean field
            UpdateAllPlayers();
            UpdateBall();
            UpdateParticles();
            UpdateCrowd();

            // Track task completion per step
            if (!taskDone)
            {
                switch (practiceStep)
                {
                    case 0: // Defender: move to left side of field
                        if (bluePlayers[0].X < FL + FW * 0.3f)
                        { taskDone = true; taskDoneTimer = 120; }
                        break;
                    case 1: // Scorer: hit the ball
                        if (bluePlayers[1].IsHitting && PDist(bluePlayers[1], ball.X, ball.Y) < 60f)
                        { taskDone = true; taskDoneTimer = 120; }
                        break;
                    case 2: // Captain: reach centre circle
                        float cx = W / 2f, cy = (FT + FB) / 2f;
                        if (PDist(bluePlayers[2], cx, cy) < 80f)
                        { taskDone = true; taskDoneTimer = 120; }
                        break;
                    case 3: // Helper: switch to helper and hit ball toward goal
                        if (bluePlayers[3].IsHitting && ball.VX > 3f)
                        { taskDone = true; taskDoneTimer = 120; }
                        break;
                }
            }
            else
            {
                taskDoneTimer--;
                if (taskDoneTimer <= 0)
                {
                    // Move to next step
                    practiceStep++;
                    if (practiceStep >= 4)
                    {
                        // All steps done — go to real game
                        practiceActive = false;
                        StartGame();
                        return;
                    }
                    // Set up next step
                    practiceWaiting = true;
                    taskDone = false;
                    activeBlueIndex = practiceStep;
                    MarkActivePlayer();
                    PlacePlayersKickoff();
                    ball.X = W / 2f; ball.Y = (FT + FB) / 2f;
                    ball.VX = 0f; ball.VY = 0f;
                }
            }
        }

        // ================================================================
        void StartGame()
        {
            SetAITuning();
            BuildCrowd();
            currentRound = 0;
            blueRoundWins = 0;
            redRoundWins = 0;
            blueGoals = 0;
            redGoals = 0;
            BuildTeams();

            // ── DB: insert match, store returned match_id ────────────────
            currentMatchId = DatabaseHelper.InsertMatch(
                currentUserId,
                aiLevel.ToString(),   // "Low" / "Medium" / "Hard"
                maxRounds);

            StartRound();
            state = GameState.Playing;
        }

        void BuildTeams()
        {
            bluePlayers.Clear();
            redPlayers.Clear();

            // Blue team (human)
            string[] blueNames = { "DUKE", "BOLT", "SWIFT", "ACE" };
            Role[] roles = { Role.Defender, Role.Scorer, Role.Captain, Role.Helper };
            Color[] bCols = {
                Color.FromArgb(30, 110, 220),
                Color.FromArgb(20,  90, 200),
                Color.FromArgb(10,  70, 180),
                Color.FromArgb(40, 130, 240)
            };
            Color[] bodyBlu = {
                Color.FromArgb(210, 170, 120),
                Color.FromArgb(195, 155, 105),
                Color.FromArgb(220, 180, 130),
                Color.FromArgb(200, 160, 110)
            };

            for (int i = 0; i < 4; i++)
            {
                Player p = new Player();
                p.Side = Team.Blue;
                p.Role = roles[i];
                p.Name = blueNames[i];
                p.Number = i + 1;
                p.JerseyColor = bCols[i];
                p.BodyColor = bodyBlu[i];
                p.HelmetColor = Color.FromArgb(20, 20, 80);
                bluePlayers.Add(p);
            }

            // Red team (AI)
            string[] redNames = { "IRON", "FURY", "BLAZE", "STORM" };
            Color[] rCols = {
                Color.FromArgb(200, 30,  30),
                Color.FromArgb(180, 20,  20),
                Color.FromArgb(220, 40,  40),
                Color.FromArgb(190, 25,  25)
            };
            Color[] bodyRed = {
                Color.FromArgb(180, 130,  90),
                Color.FromArgb(165, 120,  80),
                Color.FromArgb(190, 140, 100),
                Color.FromArgb(175, 125,  85)
            };

            for (int i = 0; i < 4; i++)
            {
                Player p = new Player();
                p.Side = Team.Red;
                p.Role = roles[i];
                p.Name = redNames[i];
                p.Number = i + 1;
                p.JerseyColor = rCols[i];
                p.BodyColor = bodyRed[i];
                p.HelmetColor = Color.FromArgb(80, 10, 10);
                redPlayers.Add(p);
            }

            activeBlueIndex = 1; // start controlling Scorer
            MarkActivePlayer();
        }

        void StartRound()
        {
            currentRound++;
            timeLeft = roundSeconds;
            tickAccum = 0;
            parts.Clear();
            PlacePlayersKickoff();
            ball.X = W / 2f; ball.Y = (FT + FB) / 2f;
            ball.VX = 0f; ball.VY = 0f;

            // reset trackers each round
            lastHitPlayer = null;
            goalJustScored = false;

            // ── DB: create round row now so goals can reference it ───────
            currentRoundId = DatabaseHelper.InsertRound(
                currentMatchId, currentRound,
                0, 0, "draw", 0);   // placeholder values, updated at EndRound
        }

        void PlacePlayersKickoff()
        {
            // Blue (left side) formation
            float[][] bluePos = {
                new float[]{ FL + 80,  (FT+FB)/2f },   // Defender (back)
                new float[]{ FL + 260, (FT+FB)/2f - 50 }, // Scorer
                new float[]{ FL + 220, (FT+FB)/2f + 50 }, // Captain
                new float[]{ FL + 190, (FT+FB)/2f }    // Helper
            };
            // Red (right side) formation
            float[][] redPos = {
                new float[]{ FR - 80,  (FT+FB)/2f },
                new float[]{ FR - 260, (FT+FB)/2f - 50 },
                new float[]{ FR - 220, (FT+FB)/2f + 50 },
                new float[]{ FR - 190, (FT+FB)/2f }
            };

            for (int i = 0; i < 4; i++)
            {
                bluePlayers[i].X = bluePos[i][0];
                bluePlayers[i].Y = bluePos[i][1];
                bluePlayers[i].VX = 0f;
                bluePlayers[i].VY = 0f;
                bluePlayers[i].HitTimer = 0;
                bluePlayers[i].IsHitting = false;

                redPlayers[i].X = redPos[i][0];
                redPlayers[i].Y = redPos[i][1];
                redPlayers[i].VX = 0f;
                redPlayers[i].VY = 0f;
                redPlayers[i].HitTimer = 0;
                redPlayers[i].IsHitting = false;
            }
        }

        void MarkActivePlayer()
        {
            for (int i = 0; i < 4; i++)
                bluePlayers[i].IsActive = (i == activeBlueIndex);
        }

        // ================================================================
        //  GAME UPDATE
        // ================================================================
        void GameUpdate()
        {
            HandlePlayerInput();

            // AI for non-active blue players
            for (int i = 0; i < 4; i++)
                if (i != activeBlueIndex) RunAI(bluePlayers[i], Team.Blue);

            // AI for all red players
            foreach (Player p in redPlayers)
                RunAI(p, Team.Red);

            UpdateAllPlayers();
            UpdateBall();
            CheckGoals();
            UpdateParticles();
            UpdateCrowd();
        }

        // ── Human input ─────────────────────────────────────────────────
        void HandlePlayerInput()
        {
            Player active = bluePlayers[activeBlueIndex];
            float spd = 3.5f;
            if (keyLeft) active.VX -= spd;
            if (keyRight) active.VX += spd;
            if (keyUp) active.VY -= spd;
            if (keyDown) active.VY += spd;

            if (keyHit && active.HitTimer == 0)
            {
                active.IsHitting = true;
                active.HitTimer = 18;
                TryHit(active);
                keyHit = false;
            }
        }

        // ── AI logic ─────────────────────────────────────────────────────
        void RunAI(Player p, Team myTeam)
        {
            p.AIThinkTimer--;
            if (p.AIThinkTimer > 0) return;
            p.AIThinkTimer = (int)aiReactTicks + rng.Next(6);

            float myGoalX = (myTeam == Team.Blue) ? FL : FR;
            float enemyGoalX = (myTeam == Team.Blue) ? FR : FL;
            float goalCY = (GT + GB) / 2f;

            bool ballOnMySide = (myTeam == Team.Blue)
                ? ball.X < W / 2f
                : ball.X > W / 2f;

            float dist = PDist(p, ball.X, ball.Y);

            switch (p.Role)
            {
                case Role.Defender:
                    {
                        // Stay near own goal, intercept if ball close
                        float defX = (myTeam == Team.Blue) ? FL + 100 : FR - 100;
                        if (dist < 160f || ballOnMySide)
                        { p.TargetX = ball.X; p.TargetY = ball.Y; }
                        else
                        { p.TargetX = defX; p.TargetY = Clamp(ball.Y, GT + 20, GB - 20); }
                        break;
                    }
                case Role.Scorer:
                    {
                        // Chase ball aggressively, aim for enemy goal
                        if (dist < 120f)
                        {
                            // Position between ball and enemy goal
                            float dx = enemyGoalX - ball.X;
                            float dy = goalCY - ball.Y;
                            float dl = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (dl > 0.1f)
                            { p.TargetX = ball.X - dx / dl * 30; p.TargetY = ball.Y - dy / dl * 30; }
                        }
                        else
                        { p.TargetX = ball.X; p.TargetY = ball.Y; }
                        break;
                    }
                case Role.Captain:
                    {
                        // Strategic: hold midfield or support scorer
                        float midX = (myTeam == Team.Blue) ? FL + FW * 0.4f : FR - FW * 0.4f;
                        if (dist < 140f)
                        { p.TargetX = ball.X; p.TargetY = ball.Y; }
                        else
                        { p.TargetX = midX; p.TargetY = Clamp(ball.Y, FT + 40, FB - 40); }
                        break;
                    }
                case Role.Helper:
                    {
                        // Support scorer: get open near enemy goal or pass position
                        float helpX = (myTeam == Team.Blue) ? FL + FW * 0.6f : FR - FW * 0.6f;
                        float offset = (ball.Y > (FT + FB) / 2f) ? -60f : 60f;
                        if (dist < 100f)
                        { p.TargetX = ball.X; p.TargetY = ball.Y; }
                        else
                        { p.TargetX = helpX; p.TargetY = Clamp(ball.Y + offset, FT + 40, FB - 40); }
                        break;
                    }
            }

            // Hit ball if in range
            if (dist < aiHitRange && p.HitTimer == 0)
            {
                p.IsHitting = true;
                p.HitTimer = 18;
                TryHit(p);
            }

            // Move toward target
            float ax = p.TargetX - p.X;
            float ay = p.TargetY - p.Y;
            float al = (float)Math.Sqrt(ax * ax + ay * ay);
            if (al > 1f)
            {
                p.VX += ax / al * aiSpeed;
                p.VY += ay / al * aiSpeed;
            }
        }

        // ── Physics ──────────────────────────────────────────────────────
        void UpdateAllPlayers()
        {
            List<Player> all = new List<Player>(bluePlayers);
            all.AddRange(redPlayers);

            foreach (Player p in all)
            {
                p.VX *= 0.76f;
                p.VY *= 0.76f;

                float spd = (float)Math.Sqrt(p.VX * p.VX + p.VY * p.VY);
                float cap = 5.8f;
                if (spd > cap) { p.VX = p.VX / spd * cap; p.VY = p.VY / spd * cap; }

                p.X += p.VX; p.Y += p.VY;
                p.X = Clamp(p.X, FL + 20, FR - 20);
                p.Y = Clamp(p.Y, FT + 26, FB - 26);

                if (p.HitTimer > 0) p.HitTimer--;
                if (p.HitTimer == 0) p.IsHitting = false;
            }

            // Player-player collision
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    float dx = all[i].X - all[j].X;
                    float dy = all[i].Y - all[j].Y;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (d < 48f && d > 0.1f)
                    {
                        float push = (48f - d) * 0.14f;
                        all[i].VX += dx / d * push;
                        all[i].VY += dy / d * push;
                        all[j].VX -= dx / d * push;
                        all[j].VY -= dy / d * push;
                    }
                }
        }

        void UpdateBall()
        {
            ball.VX *= Ball.Friction;
            ball.VY *= Ball.Friction;
            ball.X += ball.VX;
            ball.Y += ball.VY;

            if (ball.Y - Ball.R < FT)
            { ball.Y = FT + Ball.R; ball.VY = (float)Math.Abs(ball.VY); SpawnBounce(ball.X, ball.Y); }
            if (ball.Y + Ball.R > FB)
            { ball.Y = FB - Ball.R; ball.VY = -(float)Math.Abs(ball.VY); SpawnBounce(ball.X, ball.Y); }

            bool inMouth = ball.Y > GT && ball.Y < GB;
            if (ball.X - Ball.R < FL && !inMouth)
            { ball.X = FL + Ball.R; ball.VX = (float)Math.Abs(ball.VX); SpawnBounce(ball.X, ball.Y); }
            if (ball.X + Ball.R > FR && !inMouth)
            { ball.X = FR - Ball.R; ball.VX = -(float)Math.Abs(ball.VX); SpawnBounce(ball.X, ball.Y); }
        }

        void TryHit(Player p)
        {
            float dx = ball.X - p.X;
            float dy = ball.Y - p.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 58f && d > 0.1f)
            {
                float power = 11f;
                if (p.Role == Role.Scorer) power = 13f;
                ball.VX += dx / d * power;
                ball.VY += dy / d * power;
                SpawnHit(ball.X, ball.Y, p.JerseyColor);

                // ── Track last hitter and increment hit count ────────────
                if (p.Side == Team.Blue)
                {
                    lastHitPlayer = p;
                    p.HitsThisMatch++;
                }
            }
        }

        void CheckGoals()
        {
            if (goalJustScored) return;   // already handled this goal, wait for reset

            bool inMouth = ball.Y > GT && ball.Y < GB;

            // Ball in LEFT goal → Red scores
            if (ball.X - Ball.R < FL - GD && inMouth)
            {
                goalJustScored = true;
                redGoals++;
                SpawnGoal();

                // ── DB: log this goal ────────────────────────────────────
                int minuteScored = (roundSeconds - timeLeft) / 60;
                DatabaseHelper.InsertGoal(
                    currentRoundId, "red", null, null, minuteScored);

                ResetAfterGoal();
            }

            // Ball in RIGHT goal → Blue scores
            if (ball.X + Ball.R > FR + GD && inMouth)
            {
                goalJustScored = true;
                blueGoals++;
                SpawnGoal();

                // ── DB: log this goal with last player who hit the ball ──
                int minuteScored = (roundSeconds - timeLeft) / 60;
                string pName = lastHitPlayer != null ? lastHitPlayer.Name : null;
                string pRole = lastHitPlayer != null ? lastHitPlayer.Role.ToString() : null;

                DatabaseHelper.InsertGoal(
                    currentRoundId, "blue", pName, pRole, minuteScored);

                if (lastHitPlayer != null)
                    lastHitPlayer.GoalsThisMatch++;

                ResetAfterGoal();
            }
        }

        void ResetAfterGoal()
        {
            ball.X = W / 2f; ball.Y = (FT + FB) / 2f;
            ball.VX = 0f; ball.VY = 0f;
            PlacePlayersKickoff();
            goalJustScored = false;   // safe to detect next goal now
        }



        void EndRound()
        {
            state = GameState.RoundOver;

            string roundWinner;
            if (blueGoals > redGoals) { blueRoundWins++; roundWinner = "blue"; roundResultMsg = "BLUE TEAM wins Round " + currentRound + "!"; }
            else if (redGoals > blueGoals) { redRoundWins++; roundWinner = "red"; roundResultMsg = "RED TEAM wins Round " + currentRound + "!"; }
            else { roundWinner = "draw"; roundResultMsg = "Round " + currentRound + " is a DRAW!"; }

            roundResultTimer = 180;

            // ── DB: update round row with final scores and duration ──────
            int duration = roundSeconds - timeLeft;
            DatabaseHelper.UpdateRound(
                currentRoundId,
                blueGoals,
                redGoals,
                roundWinner,
                duration);

            // Check if game over
            int roundsPlayed = currentRound;
            bool maxReached = roundsPlayed >= maxRounds;
            bool blueCannotWin = redRoundWins > maxRounds / 2;
            bool redCannotWin = blueRoundWins > maxRounds / 2;

            if (maxReached || blueCannotWin || redCannotWin)
            {
                // ── DB: finalize match winner ────────────────────────────
                string matchWinner;
                if (blueRoundWins > redRoundWins) matchWinner = "blue";
                else if (redRoundWins > blueRoundWins) matchWinner = "red";
                else matchWinner = "draw";

                DatabaseHelper.UpdateMatchWinner(currentMatchId, matchWinner);

                // ── DB: save each blue player's cumulative stats ─────────
                foreach (Player p in bluePlayers)
                {
                    DatabaseHelper.SavePlayerStats(
                        currentUserId,
                        p.Name,
                        p.Role.ToString(),
                        p.GoalsThisMatch,
                        p.HitsThisMatch);
                }

                state = GameState.GameOver;
            }
            else
            {
                Timer t = new Timer();
                t.Interval = 3000;
                t.Tick += (s, ev) =>
                {
                    t.Stop();
                    blueGoals = 0; redGoals = 0;
                    StartRound();
                    state = GameState.Playing;
                };
                t.Start();
            }
        }

        // ================================================================
        //  STADIUM CROWD
        // ================================================================

        void BuildCrowd()
        {
            // Fan color palette - mix of both team colors + neutrals
            fanPalette = new Color[]
            {
                Color.FromArgb(30, 110, 220),   // blue team
                Color.FromArgb(20,  90, 200),
                Color.FromArgb(80, 160, 255),
                Color.FromArgb(200, 30,  30),   // red team
                Color.FromArgb(220, 60,  60),
                Color.FromArgb(255, 100, 100),
                Color.FromArgb(240, 200,  40),  // neutrals
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(180, 180, 180),
                Color.FromArgb(20,  160,  80),
                Color.FromArgb(200, 120,  30),
                Color.FromArgb(150,  50, 200),
            };

            // We draw crowd in 3 stands: Top, Bottom, Right (no left - that's where goals are better visible)
            // Top stand : y = 0..FT,  x = FL..FR  (many rows)
            // Bottom stand: y = FB..H, x = FL..FR
            // Right stand: x = FR..W, y = FT..FB  (side stand)
            // Left stand : x = 0..FL, y = FT..FB
            List<Fan> fanList = new List<Fan>();

            // TOP stand - 3 rows
            for (int row = 0; row < 4; row++)
            {
                float fy = 8f + row * 18f;
                int count = (FR - FL) / 14;
                for (int i = 0; i < count; i++)
                {
                    Fan f = new Fan();
                    f.X = FL + i * 14f + 7f + (float)(rng.NextDouble() * 4 - 2);
                    f.Y = fy + (float)(rng.NextDouble() * 4);
                    f.BobPhase = (float)(rng.NextDouble() * Math.PI * 2);
                    f.BobSpeed = 0.04f + (float)(rng.NextDouble() * 0.04f);
                    f.ColorIdx = rng.Next(fanPalette.Length);
                    f.FlashInterval = 40 + rng.Next(120);
                    f.FlashTimer = rng.Next(f.FlashInterval);
                    f.FlashAngle = (float)(Math.PI * 0.4 + rng.NextDouble() * Math.PI * 0.2); // angled down toward field
                    fanList.Add(f);
                }
            }

            // BOTTOM stand - 3 rows
            for (int row = 0; row < 4; row++)
            {
                float fy = H - 10f - row * 18f;
                int count = (FR - FL) / 14;
                for (int i = 0; i < count; i++)
                {
                    Fan f = new Fan();
                    f.X = FL + i * 14f + 7f + (float)(rng.NextDouble() * 4 - 2);
                    f.Y = fy + (float)(rng.NextDouble() * 4);
                    f.BobPhase = (float)(rng.NextDouble() * Math.PI * 2);
                    f.BobSpeed = 0.04f + (float)(rng.NextDouble() * 0.04f);
                    f.ColorIdx = rng.Next(fanPalette.Length);
                    f.FlashInterval = 40 + rng.Next(120);
                    f.FlashTimer = rng.Next(f.FlashInterval);
                    f.FlashAngle = -(float)(Math.PI * 0.4 + rng.NextDouble() * Math.PI * 0.2); // angled up toward field
                    fanList.Add(f);
                }
            }

            // LEFT stand - 3 cols
            for (int col = 0; col < 3; col++)
            {
                float fx = 8f + col * 18f;
                int count = (FB - FT) / 14;
                for (int i = 0; i < count; i++)
                {
                    Fan f = new Fan();
                    f.X = fx + (float)(rng.NextDouble() * 4);
                    f.Y = FT + i * 14f + 7f + (float)(rng.NextDouble() * 4 - 2);
                    f.BobPhase = (float)(rng.NextDouble() * Math.PI * 2);
                    f.BobSpeed = 0.04f + (float)(rng.NextDouble() * 0.04f);
                    f.ColorIdx = rng.Next(fanPalette.Length);
                    f.FlashInterval = 40 + rng.Next(120);
                    f.FlashTimer = rng.Next(f.FlashInterval);
                    f.FlashAngle = (float)(rng.NextDouble() * 0.4 - 0.2); // pointing right toward field
                    fanList.Add(f);
                }
            }

            // RIGHT stand - 3 cols
            for (int col = 0; col < 3; col++)
            {
                float fx = W - 8f - col * 18f;
                int count = (FB - FT) / 14;
                for (int i = 0; i < count; i++)
                {
                    Fan f = new Fan();
                    f.X = fx + (float)(rng.NextDouble() * 4);
                    f.Y = FT + i * 14f + 7f + (float)(rng.NextDouble() * 4 - 2);
                    f.BobPhase = (float)(rng.NextDouble() * Math.PI * 2);
                    f.BobSpeed = 0.04f + (float)(rng.NextDouble() * 0.04f);
                    f.ColorIdx = rng.Next(fanPalette.Length);
                    f.FlashInterval = 40 + rng.Next(120);
                    f.FlashTimer = rng.Next(f.FlashInterval);
                    f.FlashAngle = (float)(Math.PI + rng.NextDouble() * 0.4 - 0.2); // pointing left
                    fanList.Add(f);
                }
            }

            fans = fanList.ToArray();
        }

        void UpdateCrowd()
        {
            if (fans == null) return;
            if (crowdExcite > 0) crowdExcite--;

            float exciteMul = (crowdExcite > 0) ? 2.5f : 1.0f;

            for (int i = 0; i < fans.Length; i++)
            {
                fans[i].BobPhase += fans[i].BobSpeed * exciteMul;

                fans[i].FlashTimer--;
                if (fans[i].FlashTimer <= 0)
                {
                    fans[i].FlashOn = !fans[i].FlashOn;
                    // When excited, flashes fire much more rapidly
                    int baseInterval = crowdExcite > 0
                        ? (6 + rng.Next(18))
                        : fans[i].FlashInterval;
                    fans[i].FlashTimer = baseInterval;
                }
            }
        }

        void DrawStadium(Graphics g)
        {
            if (fans == null) return;

            // ── Stand backgrounds ──────────────────────────────────────
            // Top stand
            SolidBrush standTop = new SolidBrush(Color.FromArgb(255, 22, 26, 40));
            g.FillRectangle(standTop, 0, 0, W, FT);
            standTop.Dispose();

            // Bottom stand
            SolidBrush standBot = new SolidBrush(Color.FromArgb(255, 22, 26, 40));
            g.FillRectangle(standBot, 0, FB, W, H - FB);
            standBot.Dispose();

            // Left stand
            SolidBrush standL = new SolidBrush(Color.FromArgb(255, 22, 26, 40));
            g.FillRectangle(standL, 0, FT, FL, FH);
            standL.Dispose();

            // Right stand
            SolidBrush standR = new SolidBrush(Color.FromArgb(255, 22, 26, 40));
            g.FillRectangle(standR, FR, FT, W - FR, FH);
            standR.Dispose();

            // Seat rows tint (subtle row lines)
            Pen rowPen = new Pen(Color.FromArgb(25, 255, 255, 255), 1f);
            // Top rows
            for (int row = 0; row < 4; row++)
                g.DrawLine(rowPen, FL, 4 + row * 18, FR, 4 + row * 18);
            // Bottom rows
            for (int row = 0; row < 4; row++)
                g.DrawLine(rowPen, FL, H - 4 - row * 18, FR, H - 4 - row * 18);
            // Left cols
            for (int col = 0; col < 3; col++)
                g.DrawLine(rowPen, 4 + col * 18, FT, 4 + col * 18, FB);
            // Right cols
            for (int col = 0; col < 3; col++)
                g.DrawLine(rowPen, W - 4 - col * 18, FT, W - 4 - col * 18, FB);
            rowPen.Dispose();

            // Stadium light pylons (corners)
            DrawPylon(g, FL - 5, FT - 5);
            DrawPylon(g, FR + 5, FT - 5);
            DrawPylon(g, FL - 5, FB + 5);
            DrawPylon(g, FR + 5, FB + 5);

            // ── Draw flashlight beams first (behind fans) ──────────────
            for (int i = 0; i < fans.Length; i++)
            {
                if (!fans[i].FlashOn) continue;
                DrawFlashBeam(g, fans[i]);
            }

            // ── Draw fans ──────────────────────────────────────────────
            for (int i = 0; i < fans.Length; i++)
            {
                DrawFan(g, fans[i]);
            }

            // ── Stadium edge / fence ───────────────────────────────────
            Pen fencePen = new Pen(Color.FromArgb(180, 200, 200, 220), 3f);
            g.DrawRectangle(fencePen, FL, FT, FW, FH);
            fencePen.Dispose();

            // Advertising boards along the fence
            DrawAdBoards(g);
        }

        void DrawPylon(Graphics g, int x, int y)
        {
            // Pylon pole
            Pen polePen = new Pen(Color.FromArgb(200, 180, 180, 180), 3f);
            g.DrawLine(polePen, x, y, x, y - 28);
            polePen.Dispose();

            // Light head
            SolidBrush lightHead = new SolidBrush(Color.FromArgb(255, 255, 240, 180));
            g.FillEllipse(lightHead, x - 5, y - 34, 10, 10);
            lightHead.Dispose();

            // Glow around pylon light
            for (int r2 = 3; r2 >= 1; r2--)
            {
                int alpha = 30 * r2;
                SolidBrush glow = new SolidBrush(Color.FromArgb(alpha, 255, 240, 150));
                g.FillEllipse(glow, x - r2 * 5, y - 34 - r2 * 2, r2 * 10, r2 * 10);
                glow.Dispose();
            }
        }

        void DrawFlashBeam(Graphics g, Fan f)
        {
            // Draw a soft translucent cone/beam from the fan toward the field
            float beamLen = 80f;
            float beamWidth = 18f;

            float tipX = f.X;
            float tipY = f.Y;
            float endX = f.X + (float)Math.Cos(f.FlashAngle) * beamLen;
            float endY = f.Y + (float)Math.Sin(f.FlashAngle) * beamLen;

            float perpX = -(float)Math.Sin(f.FlashAngle);
            float perpY = (float)Math.Cos(f.FlashAngle);

            PointF[] cone = new PointF[]
            {
                new PointF(tipX, tipY),
                new PointF(endX + perpX * beamWidth, endY + perpY * beamWidth),
                new PointF(endX - perpX * beamWidth, endY - perpY * beamWidth),
            };

            // Soft yellow/white beam
            int beamAlpha = crowdExcite > 0 ? 28 : 14;
            SolidBrush beamBr = new SolidBrush(Color.FromArgb(beamAlpha, 255, 250, 200));
            g.FillPolygon(beamBr, cone);
            beamBr.Dispose();

            // Bright dot at source
            SolidBrush dotBr = new SolidBrush(Color.FromArgb(200, 255, 255, 220));
            g.FillEllipse(dotBr, tipX - 2f, tipY - 2f, 4f, 4f);
            dotBr.Dispose();
        }

        void DrawFan(Graphics g, Fan f)
        {
            Color jColor = fanPalette[f.ColorIdx];
            float bob = (float)Math.Sin(f.BobPhase) * 2.5f;

            float fx = f.X;
            float fy = f.Y + bob;

            // Body (small oval)
            SolidBrush bodyBr = new SolidBrush(jColor);
            g.FillEllipse(bodyBr, fx - 5f, fy - 2f, 10f, 9f);
            bodyBr.Dispose();

            // Head (tiny circle)
            SolidBrush headBr = new SolidBrush(Color.FromArgb(220, 185, 145));
            g.FillEllipse(headBr, fx - 3.5f, fy - 9f, 7f, 7f);
            headBr.Dispose();

            // Arms raised if excited
            if (crowdExcite > 0)
            {
                Pen armPen = new Pen(jColor, 1.5f);
                float armRaise = (float)Math.Sin(f.BobPhase * 1.5f) * 3f;
                g.DrawLine(armPen, fx - 5f, fy, fx - 9f, fy - 5f - armRaise);
                g.DrawLine(armPen, fx + 5f, fy, fx + 9f, fy - 5f + armRaise);
                armPen.Dispose();
            }
            else
            {
                // Normal arms resting
                Pen armPen = new Pen(jColor, 1.5f);
                g.DrawLine(armPen, fx - 5f, fy + 1f, fx - 8f, fy + 4f);
                g.DrawLine(armPen, fx + 5f, fy + 1f, fx + 8f, fy + 4f);
                armPen.Dispose();
            }

            // Flashlight dot (bright point when on)
            if (f.FlashOn)
            {
                SolidBrush flashDot = new SolidBrush(Color.FromArgb(255, 255, 255, 200));
                g.FillEllipse(flashDot, fx - 1.5f, fy - 1.5f, 3f, 3f);
                flashDot.Dispose();
            }
        }

        void DrawAdBoards(Graphics g)
        {
            // Advertising boards along field edges (colorful banners)
            string[] ads = { "HORSE CUP", "POLO SPORT", "FAST FEED", "GO TEAM", "CHEER UP", "MVP GEAR" };
            Color[] adCols = {
                Color.FromArgb(200, 220,  40,  40),
                Color.FromArgb(200,  40, 120, 220),
                Color.FromArgb(200, 230, 160,  20),
                Color.FromArgb(200,  40, 180,  80),
                Color.FromArgb(200, 180,  40, 200),
                Color.FromArgb(200,  40, 200, 200),
            };

            int boardW = 100, boardH = 14;
            int count = FW / boardW;
            Font adFont = new Font("Arial", 5.5f, FontStyle.Bold);

            for (int i = 0; i < count; i++)
            {
                int bx = FL + i * boardW;
                Color bc = adCols[i % adCols.Length];

                // Top board
                SolidBrush bb = new SolidBrush(bc);
                g.FillRectangle(bb, bx, FT - boardH - 1, boardW - 2, boardH);
                bb.Dispose();
                SolidBrush adTxt = new SolidBrush(Color.White);
                g.DrawString(ads[i % ads.Length], adFont, adTxt, bx + 4, FT - boardH + 2);
                adTxt.Dispose();

                // Bottom board
                SolidBrush bb2 = new SolidBrush(adCols[(i + 3) % adCols.Length]);
                g.FillRectangle(bb2, bx, FB + 1, boardW - 2, boardH);
                bb2.Dispose();
                SolidBrush adTxt2 = new SolidBrush(Color.White);
                g.DrawString(ads[(i + 2) % ads.Length], adFont, adTxt2, bx + 4, FB + 3);
                adTxt2.Dispose();
            }

            // Side boards (left and right)
            int sideCount = FH / boardW;
            for (int i = 0; i < sideCount; i++)
            {
                int by = FT + i * boardW;
                // Left board
                SolidBrush sb = new SolidBrush(adCols[(i + 1) % adCols.Length]);
                g.FillRectangle(sb, FL - boardH - 1, by, boardH, boardW - 2);
                sb.Dispose();
                // Right board
                SolidBrush sb2 = new SolidBrush(adCols[(i + 4) % adCols.Length]);
                g.FillRectangle(sb2, FR + 1, by, boardH, boardW - 2);
                sb2.Dispose();
            }

            adFont.Dispose();
        }

        // ================================================================
        //  PARTICLES

        void SpawnBounce(float x, float y)
        { for (int i = 0; i < 4; i++) parts.Add(new Particle(x, y, Color.White, rng, 2.5f)); }

        void SpawnHit(float x, float y, Color c)
        { for (int i = 0; i < 10; i++) parts.Add(new Particle(x, y, c, rng, 4f)); }

        void SpawnGoal()
        {
            for (int i = 0; i < 80; i++)
            {
                Color c = Color.FromArgb(rng.Next(256), rng.Next(256), rng.Next(256));
                parts.Add(new Particle(W / 2f, (FT + FB) / 2f, c, rng, 7f));
            }
            crowdExcite = 300;   // fans go wild for 5 seconds
        }

        void UpdateParticles()
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            { parts[i].Update(); if (parts[i].Dead) parts.RemoveAt(i); }
        }

        // ================================================================
        //  DRAW
        // ================================================================
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            switch (state)
            {
                case GameState.Menu: DrawMenu(g); break;
                case GameState.Playing:
                case GameState.RoundOver: DrawGame(g); break;
                case GameState.Practice: DrawGame(g); break;
                case GameState.Paused: DrawGame(g); DrawPauseOverlay(g); break;
                case GameState.GameOver: DrawGame(g); DrawGameOver(g); break;
            }
        }

        // ── MENU ─────────────────────────────────────────────────────────
        void DrawMenu(Graphics g)
        {
            SolidBrush bg = new SolidBrush(Color.FromArgb(15, 18, 30));
            g.FillRectangle(bg, 0, 0, W, H);
            bg.Dispose();

            SolidBrush fieldBr = new SolidBrush(Color.FromArgb(25, 80, 35));
            g.FillRectangle(fieldBr, 0, H - 100, W, 100);
            fieldBr.Dispose();

            // Title
            Font titleF = new Font("Impact", 52f, FontStyle.Regular);
            DrawShadow(g, "HORSE", titleF, Color.FromArgb(255, 210, 30), W / 2f - 155, 45f);
            DrawShadow(g, "HOCKEY", titleF, Color.FromArgb(30, 160, 255), W / 2f + 10, 45f);
            titleF.Dispose();

            Font subF = new Font("Segoe UI", 13f, FontStyle.Bold);
            DrawShadow(g, "4  vs  4   |   Up to 6 Rounds   |   7 Minutes each", subF,
                Color.FromArgb(180, 180, 180), W / 2f, 138f, true);
            subF.Dispose();

            if (menuMode == 0)
            {
                // ── PAGE 1: Mode Selection (Practice or Play) ──────────────
                Font mTitle = new Font("Segoe UI", 17f, FontStyle.Bold);
                DrawShadow(g, "SELECT  GAME  MODE", mTitle, Color.FromArgb(255, 200, 50), W / 2f, 190f, true);
                mTitle.Dispose();

                // Practice box
                bool pracSel = (menuSel == 0);
                DrawMenuBox(g,
                    W / 2 - 260, 240, 520, 110,
                    pracSel,
                    Color.FromArgb(50, 200, 140),
                    "PRACTICE  MODE",
                    "Learn each player role step by step",
                    "Defender  |  Scorer  |  Captain  |  Helper",
                    22f);

                // Play box
                bool playSel = (menuSel == 1);
                DrawMenuBox(g,
                    W / 2 - 260, 370, 520, 110,
                    playSel,
                    Color.FromArgb(30, 140, 255),
                    "PLAY  DIRECTLY",
                    "Jump straight into the full match",
                    "Choose difficulty on next screen",
                    22f);

                Font arrF2 = new Font("Segoe UI", 12f, FontStyle.Bold);
                SolidBrush arrBr2 = new SolidBrush(Color.FromArgb(180, 200, 200));
                DrawShadow(g, "UP / DOWN  to select     ENTER to confirm", arrF2,
                    Color.FromArgb(180, 200, 200), W / 2f, 500f, true);
                arrF2.Dispose(); arrBr2.Dispose();
            }
            else
            {
                // ── PAGE 2: Difficulty Selection ────────────────────────────
                Font dTitle = new Font("Segoe UI", 16f, FontStyle.Bold);
                DrawShadow(g, "SELECT  AI  DIFFICULTY", dTitle,
                    Color.FromArgb(255, 200, 50), W / 2f, 195f, true);
                dTitle.Dispose();

                string[] labels = { "LOW", "MEDIUM", "HARD" };
                Color[] dColors = {
                    Color.FromArgb(50, 200, 80),
                    Color.FromArgb(255, 180, 30),
                    Color.FromArgb(220, 50, 50)
                };
                string[] dDesc1 = { "Relaxed AI  -  Great for beginners", "Balanced AI  -  Fair challenge", "Elite AI  -  Plays to win" };
                string[] dDesc2 = { "Slow reactions, wide formation", "Smart positioning, medium speed", "Aggressive, fast, hard to beat" };

                for (int i = 0; i < 3; i++)
                {
                    bool sel = (i == menuSel);
                    DrawMenuBox(g, W / 2 - 200, 235 + i * 90, 400, 76,
                        sel, dColors[i], labels[i], dDesc1[i], dDesc2[i], sel ? 24f : 20f);
                }

                Font arrF3 = new Font("Segoe UI", 12f, FontStyle.Bold);
                DrawShadow(g, "UP / DOWN to select     ENTER to start     Esc = back", arrF3,
                    Color.FromArgb(180, 200, 200), W / 2f, 515f, true);
                arrF3.Dispose();
            }

            Font ctrlF = new Font("Segoe UI", 8.5f);
            SolidBrush ctrlBr = new SolidBrush(Color.FromArgb(100, 100, 120));
            SizeF csz = g.MeasureString("Arrow Keys=Move  |  Space=Hit  |  Tab=Switch Player  |  P=Pause", ctrlF);
            g.DrawString("Arrow Keys=Move  |  Space=Hit  |  Tab=Switch Player  |  P=Pause",
                ctrlF, ctrlBr, W / 2f - csz.Width / 2f, 570f);
            ctrlF.Dispose(); ctrlBr.Dispose();
        }

        void DrawMenuBox(Graphics g, int bx, int by, int bw, int bh,
            bool selected, Color accent, string title, string line1, string line2, float titleSize)
        {
            SolidBrush boxBg = selected
                ? new SolidBrush(Color.FromArgb(55, accent.R, accent.G, accent.B))
                : new SolidBrush(Color.FromArgb(25, 35, 50));
            g.FillRectangle(boxBg, bx, by, bw, bh);
            boxBg.Dispose();

            Pen boxPen = new Pen(selected ? accent : Color.FromArgb(55, 70, 90), selected ? 3f : 1.5f);
            g.DrawRectangle(boxPen, bx, by, bw, bh);
            boxPen.Dispose();

            if (selected)
            {
                // Glowing left bar
                SolidBrush barBr = new SolidBrush(accent);
                g.FillRectangle(barBr, bx, by, 5, bh);
                barBr.Dispose();
            }

            Font tf = new Font("Impact", titleSize, FontStyle.Regular);
            DrawShadow(g, title, tf, selected ? accent : Color.FromArgb(140, 140, 140),
                bx + bw / 2f, by + 8f, true);
            tf.Dispose();

            Font lf1 = new Font("Segoe UI", 9f);
            SolidBrush lb1 = new SolidBrush(selected ? Color.FromArgb(210, 210, 210) : Color.FromArgb(100, 100, 100));
            SizeF ls1 = g.MeasureString(line1, lf1);
            g.DrawString(line1, lf1, lb1, bx + bw / 2f - ls1.Width / 2f, by + bh - 38f);
            lf1.Dispose(); lb1.Dispose();

            Font lf2 = new Font("Segoe UI", 8f, FontStyle.Italic);
            SolidBrush lb2 = new SolidBrush(selected ? Color.FromArgb(160, 180, 160) : Color.FromArgb(70, 70, 70));
            SizeF ls2 = g.MeasureString(line2, lf2);
            g.DrawString(line2, lf2, lb2, bx + bw / 2f - ls2.Width / 2f, by + bh - 20f);
            lf2.Dispose(); lb2.Dispose();
        }

        // ── GAME ─────────────────────────────────────────────────────────
        void DrawGame(Graphics g)
        {
            SolidBrush bg = new SolidBrush(Color.FromArgb(15, 18, 30));
            g.FillRectangle(bg, 0, 0, W, H);
            bg.Dispose();

            DrawStadium(g);
            DrawField(g);
            DrawParticles(g);
            DrawBall(g);

            foreach (Player p in redPlayers) DrawPlayer(g, p);
            foreach (Player p in bluePlayers) DrawPlayer(g, p);

            DrawHUD(g);

            if (state == GameState.RoundOver && roundResultTimer > 0)
                DrawRoundResult(g);
            if (state == GameState.Practice)
                DrawPracticeOverlay(g);
        }

        // ── Practice overlay ──────────────────────────────────────────────
        void DrawPracticeOverlay(Graphics g)
        {
            string[] roleNames = { "DEFENDER", "SCORER", "CAPTAIN", "HELPER" };
            string[] playerNames = { "DUKE", "BOLT", "SWIFT", "ACE" };
            Color[] roleColors = {
                Color.FromArgb(80,  180, 255),   // Defender  - blue
                Color.FromArgb(255, 80,  80),    // Scorer    - red
                Color.FromArgb(255, 210, 30),    // Captain   - gold
                Color.FromArgb(80,  220, 140),   // Helper    - green
            };

            string[][] stepLines = new string[][]
            {
                new string[]
                {
                    "You are now controlling  DUKE  —  the  DEFENDER",
                    "The Defender protects your goal on the LEFT side.",
                    "OBJECTIVE:  Move your horse to the left side of the field.",
                    "Use  Arrow Keys  to move.   Press  Space  to hit the ball.",
                },
                new string[]
                {
                    "You are now controlling  BOLT  —  the  SCORER",
                    "The Scorer is your main attacker — score goals on the RIGHT.",
                    "OBJECTIVE:  Hit the ball with your mallet!",
                    "Move close to the ball and press  Space  to swing your mallet.",
                },
                new string[]
                {
                    "You are now controlling  SWIFT  —  the  CAPTAIN",
                    "The Captain controls the midfield and plans strategy.",
                    "OBJECTIVE:  Ride to the centre circle of the field.",
                    "Use  Arrow Keys  to navigate.   Tab = switch player anytime.",
                },
                new string[]
                {
                    "You are now controlling  ACE  —  the  HELPER",
                    "The Helper supports the Scorer with passes and assists.",
                    "OBJECTIVE:  Hit the ball forward (toward the right goal).",
                    "Get behind the ball and swing your mallet to the right!",
                },
            };

            string[] completedLines = {
                "GREAT!  You held the defensive line!",
                "PERFECT SHOT!  That is how you score!",
                "EXCELLENT!  You own the midfield!",
                "BRILLIANT PASS!  Your Scorer will love you!",
            };

            Color roleCol = roleColors[practiceStep];
            int step = practiceStep;

            // ── WAITING panel (before user starts step) ─────────────────
            if (practiceWaiting)
            {
                // Dark overlay at bottom
                SolidBrush ov = new SolidBrush(Color.FromArgb(220, 10, 14, 26));
                g.FillRectangle(ov, 0, FB + 2, W, H - FB - 2);
                ov.Dispose();

                // Full centre panel
                int px = 80, py2 = 90, pw = W - 160, ph = FB - 110;
                SolidBrush panelBg = new SolidBrush(Color.FromArgb(210, 10, 14, 26));
                g.FillRectangle(panelBg, px, py2, pw, ph);
                panelBg.Dispose();

                // Colour accent top bar
                SolidBrush accentBar = new SolidBrush(roleCol);
                g.FillRectangle(accentBar, px, py2, pw, 6);
                accentBar.Dispose();

                Pen panelPen = new Pen(roleCol, 2f);
                g.DrawRectangle(panelPen, px, py2, pw, ph);
                panelPen.Dispose();

                // Step badge
                Font badgeF = new Font("Impact", 11f);
                string badge = "STEP  " + (step + 1) + "  OF  4";
                SolidBrush badgeBr = new SolidBrush(roleCol);
                SizeF bsz = g.MeasureString(badge, badgeF);
                g.DrawString(badge, badgeF, badgeBr, W / 2f - bsz.Width / 2f, py2 + 14f);
                badgeF.Dispose(); badgeBr.Dispose();

                // Role name
                Font roleF = new Font("Impact", 38f);
                DrawShadow(g, roleNames[step], roleF, roleCol, W / 2f, py2 + 38f, true);
                roleF.Dispose();

                // Player name
                Font pnF = new Font("Segoe UI", 13f, FontStyle.Bold);
                DrawShadow(g, "Player:  " + playerNames[step], pnF,
                    Color.FromArgb(200, 200, 200), W / 2f, py2 + 105f, true);
                pnF.Dispose();

                // Divider
                Pen divPen = new Pen(Color.FromArgb(60, roleCol.R, roleCol.G, roleCol.B), 1f);
                g.DrawLine(divPen, px + 40, py2 + 135, px + pw - 40, py2 + 135);
                divPen.Dispose();

                // Description lines
                Font lineF = new Font("Segoe UI", 11f);
                float ly = py2 + 150f;
                foreach (string line in stepLines[step])
                {
                    bool isObj = line.StartsWith("OBJECTIVE");
                    Color lc = isObj ? roleCol : Color.FromArgb(200, 200, 200);
                    Font lFont = isObj
                        ? new Font("Segoe UI", 11f, FontStyle.Bold)
                        : new Font("Segoe UI", 11f);
                    SizeF lsz = g.MeasureString(line, lFont);
                    SolidBrush lBr = new SolidBrush(lc);
                    g.DrawString(line, lFont, lBr, W / 2f - lsz.Width / 2f, ly);
                    lBr.Dispose(); lFont.Dispose();
                    ly += 30f;
                }
                lineF.Dispose();

                // Flashing PRESS SPACE button
                bool flash = ((practiceArrowAnim / 25) % 2 == 0);
                if (flash)
                {
                    Font spaceF = new Font("Impact", 20f);
                    SolidBrush spaceBr = new SolidBrush(Color.White);
                    SolidBrush spaceBg = new SolidBrush(Color.FromArgb(180, roleCol.R, roleCol.G, roleCol.B));
                    string spaceMsg = "  PRESS  SPACE  TO  BEGIN  THIS  STEP  ";
                    SizeF ssz = g.MeasureString(spaceMsg, spaceF);
                    float sx = W / 2f - ssz.Width / 2f;
                    float sy = py2 + ph - 52f;
                    g.FillRectangle(spaceBg, sx - 4, sy - 2, ssz.Width + 8, ssz.Height + 4);
                    g.DrawString(spaceMsg, spaceF, spaceBr, sx, sy);
                    spaceF.Dispose(); spaceBr.Dispose(); spaceBg.Dispose();
                }

                // Progress dots
                for (int i = 0; i < 4; i++)
                {
                    Color dotC = (i == step) ? roleCol
                               : (i < step) ? Color.FromArgb(100, roleCol.R, roleCol.G, roleCol.B)
                               : Color.FromArgb(50, 60, 70);
                    SolidBrush dotBr = new SolidBrush(dotC);
                    g.FillEllipse(dotBr, W / 2f - 36 + i * 24, py2 + ph - 18f, 14f, 14f);
                    dotBr.Dispose();
                }
            }
            else if (taskDone)
            {
                // ── COMPLETED banner ────────────────────────────────────
                SolidBrush ov2 = new SolidBrush(Color.FromArgb(160, 10, 14, 26));
                g.FillRectangle(ov2, 0, 0, W, H);
                ov2.Dispose();

                Font doneF = new Font("Impact", 40f);
                DrawShadow(g, completedLines[step], doneF, roleCol, W / 2f, H / 2f - 50f, true);
                doneF.Dispose();

                bool nextExists = (step < 3);
                Font nextF = new Font("Segoe UI", 14f, FontStyle.Bold);
                string nextMsg = nextExists
                    ? "Next:  " + roleNames[step + 1] + "  (" + playerNames[step + 1] + ")  —  Loading..."
                    : "Practice Complete!  Starting the real match...";
                DrawShadow(g, nextMsg, nextF, Color.White, W / 2f, H / 2f + 20f, true);
                nextF.Dispose();

                // Progress dots
                for (int i = 0; i < 4; i++)
                {
                    Color dotC = (i <= step) ? roleColors[i] : Color.FromArgb(50, 60, 70);
                    SolidBrush dotBr = new SolidBrush(dotC);
                    g.FillEllipse(dotBr, W / 2f - 36 + i * 24, H / 2f + 70f, 18f, 18f);
                    dotBr.Dispose();
                }
            }
            else
            {
                // ── IN-PROGRESS mini HUD bar at top ─────────────────────
                SolidBrush hudOv = new SolidBrush(Color.FromArgb(200, 10, 14, 26));
                g.FillRectangle(hudOv, 0, 0, W, 56);
                hudOv.Dispose();

                SolidBrush acBar = new SolidBrush(roleCol);
                g.FillRectangle(acBar, 0, 53, W, 3);
                acBar.Dispose();

                Font hf = new Font("Impact", 16f);
                DrawShadow(g, "PRACTICE  —  " + roleNames[step] + "  (" + playerNames[step] + ")",
                    hf, roleCol, W / 2f, 6f, true);
                hf.Dispose();

                // Objective reminder
                Font objF = new Font("Segoe UI", 9f, FontStyle.Bold);
                string obj = stepLines[step][2]; // the OBJECTIVE line
                SizeF objSz = g.MeasureString(obj, objF);
                SolidBrush objBr = new SolidBrush(Color.FromArgb(200, 200, 200));
                g.DrawString(obj, objF, objBr, W / 2f - objSz.Width / 2f, 32f);
                objF.Dispose(); objBr.Dispose();

                // Bottom hint bar
                SolidBrush botOv = new SolidBrush(Color.FromArgb(180, 10, 14, 26));
                g.FillRectangle(botOv, 0, FB + 2, W, H - FB - 2);
                botOv.Dispose();

                Font hintF = new Font("Segoe UI", 9f);
                SolidBrush hintBr = new SolidBrush(Color.FromArgb(150, 160, 180));
                string hint = "Arrow Keys = Move   |   Space = Hit Ball   |   Tab = Switch Player   |   Esc = Return to Main Menu";
                SizeF hintSz = g.MeasureString(hint, hintF);
                g.DrawString(hint, hintF, hintBr, W / 2f - hintSz.Width / 2f, FB + 8f);
                hintF.Dispose(); hintBr.Dispose();

                // Progress dots bottom
                for (int i = 0; i < 4; i++)
                {
                    Color dotC = (i == step) ? roleCol
                               : (i < step) ? Color.FromArgb(100, roleColors[i].R, roleColors[i].G, roleColors[i].B)
                               : Color.FromArgb(50, 60, 70);
                    SolidBrush dotBr = new SolidBrush(dotC);
                    g.FillEllipse(dotBr, W / 2f - 36 + i * 24, FB + 28f, 14f, 14f);
                    dotBr.Dispose();
                }
            }
        }

        void DrawField(Graphics g)
        {
            Rectangle fr = new Rectangle(FL, FT, FW, FH);
            LinearGradientBrush gb = new LinearGradientBrush(fr,
                Color.FromArgb(28, 85, 38), Color.FromArgb(18, 65, 28), 90f);
            g.FillRectangle(gb, fr);
            gb.Dispose();

            // Alternating grass stripes
            for (int i = 0; i < 8; i++)
            {
                int sx = FL + i * FW / 8;
                if (i % 2 == 0)
                {
                    SolidBrush stripeBr = new SolidBrush(Color.FromArgb(10, 255, 255, 255));
                    g.FillRectangle(stripeBr, sx, FT, FW / 8, FH);
                    stripeBr.Dispose();
                }
            }

            // Centre line
            Pen cPen = new Pen(Color.FromArgb(90, 255, 255, 255), 2f);
            g.DrawLine(cPen, W / 2, FT, W / 2, FB);
            g.DrawEllipse(cPen, W / 2 - 60, (FT + FB) / 2 - 60, 120, 120);
            // Centre spot
            SolidBrush cSpot = new SolidBrush(Color.FromArgb(90, 255, 255, 255));
            g.FillEllipse(cSpot, W / 2 - 4, (FT + FB) / 2 - 4, 8, 8);
            cSpot.Dispose();
            cPen.Dispose();

            // Penalty areas
            Pen paPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1.5f);
            g.DrawRectangle(paPen, FL, GT - 40, 120, (GB - GT) + 80);
            g.DrawRectangle(paPen, FR - 120, GT - 40, 120, (GB - GT) + 80);
            paPen.Dispose();

            // Field border
            Pen borderPen = new Pen(Color.FromArgb(210, 255, 255, 255), 3f);
            g.DrawRectangle(borderPen, fr);
            borderPen.Dispose();

            // Goals
            DrawGoal(g, FL - GD, GT, GD, GB - GT, true);
            DrawGoal(g, FR, GT, GD, GB - GT, false);
        }

        void DrawGoal(Graphics g, int x, int y, int d, int h, bool isLeft)
        {
            Rectangle nr = new Rectangle(x, y, d, h);
            SolidBrush nb = new SolidBrush(Color.FromArgb(35, 255, 255, 255));
            g.FillRectangle(nb, nr); nb.Dispose();

            Pen np = new Pen(Color.FromArgb(210, 255, 255, 255), 2f);
            g.DrawRectangle(np, nr); np.Dispose();

            Pen grd = new Pen(Color.FromArgb(70, 200, 200, 200), 1f);
            for (int gy = y; gy < y + h; gy += 14) g.DrawLine(grd, x, gy, x + d, gy);
            for (int gx = x; gx < x + d; gx += 14) g.DrawLine(grd, gx, y, gx, y + h);
            grd.Dispose();

            Pen pp = new Pen(Color.FromArgb(255, 230, 230, 230), 5f);
            int px = isLeft ? FL : FR;
            g.DrawLine(pp, px, y, px, y + h); pp.Dispose();

            // Goal team color accent
            Color gc = isLeft ? Color.FromArgb(60, 30, 80, 200) : Color.FromArgb(60, 200, 30, 30);
            SolidBrush gcBr = new SolidBrush(gc);
            g.FillRectangle(gcBr, nr); gcBr.Dispose();
        }

        void DrawBall(Graphics g)
        {
            float r = Ball.R;
            float bx = ball.X - r, by = ball.Y - r, bw = r * 2;

            SolidBrush sh = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            g.FillEllipse(sh, bx + 3, by + 6, bw, bw); sh.Dispose();

            GraphicsPath bp = new GraphicsPath();
            bp.AddEllipse(bx, by, bw, bw);
            PathGradientBrush pgb = new PathGradientBrush(bp);
            pgb.CenterColor = Color.White;
            pgb.SurroundColors = new Color[] { Color.FromArgb(150, 150, 150) };
            g.FillEllipse(pgb, bx, by, bw, bw);
            pgb.Dispose(); bp.Dispose();

            Pen lp = new Pen(Color.FromArgb(80, 0, 0, 0), 1f);
            g.DrawEllipse(lp, bx, by, bw, bw);
            g.DrawLine(lp, ball.X - r * 0.65f, ball.Y, ball.X + r * 0.65f, ball.Y);
            lp.Dispose();
        }

        // ── Draw one player (horse + rider) ──────────────────────────────
        void DrawPlayer(Graphics g, Player p)
        {
            bool facingRight = (p.Side == Team.Blue);

            g.TranslateTransform(p.X, p.Y);
            if (!facingRight) g.ScaleTransform(-1, 1);

            // Shadow
            SolidBrush shBr = new SolidBrush(Color.FromArgb(55, 0, 0, 0));
            g.FillEllipse(shBr, -22, 22, 44, 14); shBr.Dispose();

            SolidBrush body = new SolidBrush(p.BodyColor);

            // Body
            g.FillEllipse(body, -20, -8, 40, 24);

            // Neck
            GraphicsPath neck = new GraphicsPath();
            neck.AddPolygon(new PointF[]{
                new PointF(-7f,-8f), new PointF(7f,-8f),
                new PointF(11f,-22f), new PointF(-3f,-22f)
            });
            g.FillPath(body, neck); neck.Dispose();

            // Head
            g.FillEllipse(body, -3, -33, 20, 15);

            // Nose
            g.FillEllipse(body, 13, -28, 8, 6);

            // Nostril
            SolidBrush nostril = new SolidBrush(Color.FromArgb(60, 30, 10));
            g.FillEllipse(nostril, 16, -27, 3, 3); nostril.Dispose();

            // Eye
            SolidBrush eyeBr = new SolidBrush(Color.FromArgb(30, 15, 5));
            g.FillEllipse(eyeBr, 9, -31, 6, 6); eyeBr.Dispose();
            SolidBrush eyeW = new SolidBrush(Color.White);
            g.FillEllipse(eyeW, 10, -30, 2, 2); eyeW.Dispose();

            // Ear
            g.FillPolygon(body, new PointF[]{
                new PointF(11f,-33f), new PointF(15f,-42f), new PointF(19f,-33f)
            });

            // Mane
            SolidBrush mane = new SolidBrush(Color.FromArgb(70, 45, 15));
            for (int i = 0; i < 5; i++) g.FillEllipse(mane, -3 + i * 3, -30 - i * 2, 8, 10);
            mane.Dispose();

            // Leg animation
            float mv = (float)Math.Abs(p.VX) + (float)Math.Abs(p.VY);
            float la = 0f;
            if (mv > 0.4f) la = (float)Math.Sin(Environment.TickCount * 0.016 + p.X * 0.3) * 9f;

            Pen legPen = new Pen(p.BodyColor, 4.5f);
            legPen.StartCap = LineCap.Round; legPen.EndCap = LineCap.Round;
            g.DrawLine(legPen, -14f, 14f, -16f, 30f + la);
            g.DrawLine(legPen, -5f, 16f, -7f, 32f - la);
            g.DrawLine(legPen, 7f, 14f, 5f, 30f + la);
            g.DrawLine(legPen, 17f, 12f, 19f, 30f - la);
            legPen.Dispose();

            // Hooves
            SolidBrush hv = new SolidBrush(Color.FromArgb(45, 35, 25));
            g.FillEllipse(hv, -20f, 27f + la, 9f, 6f);
            g.FillEllipse(hv, -11f, 29f - la, 9f, 6f);
            g.FillEllipse(hv, 1f, 27f + la, 9f, 6f);
            g.FillEllipse(hv, 15f, 27f - la, 9f, 6f);
            hv.Dispose();

            // Tail
            GraphicsPath tail = new GraphicsPath();
            tail.AddCurve(new PointF[]{
                new PointF(-20f,6f), new PointF(-30f,la*0.4f),
                new PointF(-34f,14f), new PointF(-28f,22f)
            });
            Pen tailP = new Pen(Color.FromArgb(70, 45, 15), 5f);
            tailP.StartCap = LineCap.Round; tailP.EndCap = LineCap.Round;
            g.DrawPath(tailP, tail); tailP.Dispose(); tail.Dispose();

            body.Dispose();

            // ── Rider ──
            DrawRider(g, p);

            // ── Hit effect ──
            if (p.IsHitting)
            {
                float prog = 1f - (float)p.HitTimer / 18f;
                float sw = prog * 38f;
                Pen swP = new Pen(Color.FromArgb(180, 120, 50), 3.5f);
                swP.StartCap = LineCap.Round; swP.EndCap = LineCap.Round;
                g.DrawLine(swP, 10f, -6f, 10f + sw, 12f + sw * 0.5f); swP.Dispose();
                Pen glow = new Pen(Color.FromArgb(160, 255, 210, 60), 2f);
                g.DrawArc(glow, 2, 2, 42, 32, -30, (int)(prog * 85)); glow.Dispose();
            }

            g.ResetTransform();

            // ── Nametag + Role badge ──
            DrawPlayerLabel(g, p);

            // ── Active ring ──
            if (p.IsActive)
            {
                Pen ring = new Pen(Color.FromArgb(220, 255, 255, 100), 2.5f);
                ring.DashStyle = DashStyle.Dash;
                g.DrawEllipse(ring, p.X - 26, p.Y - 35, 52, 62);
                ring.Dispose();
            }
        }

        void DrawRider(Graphics g, Player p)
        {
            SolidBrush jBr = new SolidBrush(p.JerseyColor);
            g.FillRectangle(jBr, -8, -24, 16, 15); jBr.Dispose();

            // Jersey number
            Font numF = new Font("Impact", 7f);
            SolidBrush numBr = new SolidBrush(Color.White);
            g.DrawString(p.Number.ToString(), numF, numBr, -3f, -22f);
            numF.Dispose(); numBr.Dispose();

            // Skin
            SolidBrush sk = new SolidBrush(Color.FromArgb(220, 180, 140));
            g.FillEllipse(sk, -6, -37, 13, 13); sk.Dispose();

            // Helmet
            SolidBrush helm = new SolidBrush(p.HelmetColor);
            g.FillEllipse(helm, -7, -39, 14, 10); helm.Dispose();

            // Visor
            SolidBrush vis = new SolidBrush(Color.FromArgb(80, 150, 220, 255));
            g.FillRectangle(vis, -6, -33, 13, 3); vis.Dispose();

            // Arms
            float ar = p.IsHitting ? -11f : 0f;
            Pen armP = new Pen(p.JerseyColor, 3.5f);
            armP.StartCap = LineCap.Round; armP.EndCap = LineCap.Round;
            g.DrawLine(armP, -8f, -22f, -15f, -15f + ar);
            g.DrawLine(armP, 8f, -22f, 15f, -13f + ar);
            armP.Dispose();

            // Stick
            float st = p.IsHitting ? 14f : 6f;
            Pen stP = new Pen(Color.FromArgb(150, 95, 35), 2.5f);
            stP.StartCap = LineCap.Round; stP.EndCap = LineCap.Round;
            g.DrawLine(stP, 15f, -13f + ar, 24f, st);
            g.DrawLine(stP, 24f, st, 31f, st + 5f);
            stP.Dispose();

            // Legs
            Pen rlP = new Pen(Color.FromArgb(55, 55, 75), 3f);
            rlP.StartCap = LineCap.Round; rlP.EndCap = LineCap.Round;
            g.DrawLine(rlP, -5f, -11f, -10f, 6f);
            g.DrawLine(rlP, 5f, -11f, 10f, 6f);
            rlP.Dispose();
        }

        void DrawPlayerLabel(Graphics g, Player p)
        {
            string roleShort = "";
            switch (p.Role)
            {
                case Role.Defender: roleShort = "DEF"; break;
                case Role.Scorer: roleShort = "SCR"; break;
                case Role.Captain: roleShort = "CAP"; break;
                case Role.Helper: roleShort = "HLP"; break;
            }

            Font lf = new Font("Segoe UI", 7f, FontStyle.Bold);
            string label = p.Name + " [" + roleShort + "]";
            SizeF sz = new SizeF(80, 14);
            float lx = p.X - 40;
            float ly = p.Y - 60;

            SolidBrush bgBr = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            g.FillRectangle(bgBr, lx, ly, 80, 14); bgBr.Dispose();

            SolidBrush lBr = new SolidBrush(p.IsActive
                ? Color.FromArgb(255, 255, 255, 100)
                : Color.FromArgb(200, 200, 200, 200));
            sz = g.MeasureString(label, lf);
            g.DrawString(label, lf, lBr, p.X - sz.Width / 2f, ly + 1f);
            lf.Dispose(); lBr.Dispose();
        }

        // ── HUD ──────────────────────────────────────────────────────────
        void DrawHUD(Graphics g)
        {
            // Top bar
            SolidBrush hudBr = new SolidBrush(Color.FromArgb(200, 10, 14, 26));
            g.FillRectangle(hudBr, 0, 0, W, 82);
            g.FillRectangle(hudBr, 0, FB + 2, W, H - FB - 2);
            hudBr.Dispose();

            // Blue team info
            Font teamF = new Font("Impact", 15f);
            DrawShadow(g, "BLUE TEAM", teamF, Color.FromArgb(80, 140, 255), 10f, 5f);
            teamF.Dispose();

            Font scoreF = new Font("Impact", 36f);
            DrawShadow(g, blueGoals.ToString(), scoreF, Color.White, 10f, 20f);
            DrawShadow(g, redGoals.ToString(), scoreF, Color.White, W - 60f, 20f);
            scoreF.Dispose();

            Font rTeamF = new Font("Impact", 15f);
            DrawShadow(g, "RED TEAM", rTeamF, Color.FromArgb(255, 80, 80), W - 120f, 5f);
            rTeamF.Dispose();

            // Round info
            Font roundF = new Font("Segoe UI", 11f, FontStyle.Bold);
            string roundTxt = "Round " + currentRound + " / " + maxRounds;
            DrawShadow(g, roundTxt, roundF, Color.FromArgb(200, 200, 200), W / 2f, 5f, true);
            roundF.Dispose();

            // Timer
            int mm = timeLeft / 60, ss = timeLeft % 60;
            string timeTxt = mm.ToString("D2") + ":" + ss.ToString("D2");
            Color timeCol = timeLeft <= 30
                ? Color.FromArgb(255, 80, 80)
                : Color.FromArgb(255, 220, 80);
            Font timeF = new Font("Impact", 28f);
            DrawShadow(g, timeTxt, timeF, timeCol, W / 2f, 22f, true);
            timeF.Dispose();

            // Difficulty badge
            string diffTxt = "AI: " + aiLevel.ToString().ToUpper();
            Color diffCol;
            switch (aiLevel)
            {
                case Difficulty.Low: diffCol = Color.FromArgb(50, 200, 80); break;
                case Difficulty.Hard: diffCol = Color.FromArgb(220, 50, 50); break;
                default: diffCol = Color.FromArgb(255, 180, 30); break;
            }
            Font diffF = new Font("Segoe UI", 9f, FontStyle.Bold);
            DrawShadow(g, diffTxt, diffF, diffCol, W / 2f, 58f, true);
            diffF.Dispose();

            // Round wins
            Font winsF = new Font("Segoe UI", 9f);
            DrawShadow(g, "Rounds won: " + blueRoundWins, winsF, Color.FromArgb(80, 140, 255), 10f, 62f);
            string rw = "Rounds won: " + redRoundWins;
            SizeF rwSz;
            rwSz = g.MeasureString(rw, winsF);
            DrawShadow(g, rw, winsF, Color.FromArgb(255, 80, 80), W - rwSz.Width - 10f, 62f);
            winsF.Dispose();

            // Active player indicator
            Player active = bluePlayers[activeBlueIndex];
            Font actF = new Font("Segoe UI", 9f, FontStyle.Bold);
            string actTxt = "CONTROLLING:  " + active.Name + "  [" + active.Role.ToString().ToUpper() + "]  -  TAB to switch";
            SolidBrush actBr = new SolidBrush(Color.FromArgb(255, 255, 255, 100));
            SizeF actSz = g.MeasureString(actTxt, actF);
            g.DrawString(actTxt, actF, actBr, W / 2f - actSz.Width / 2f, FB + 6f);
            actF.Dispose(); actBr.Dispose();

            // Blue player status strip
            DrawTeamStrip(g, bluePlayers, 10, FB + 26, Color.FromArgb(30, 100, 220));
            // Red player status strip
            DrawTeamStrip(g, redPlayers, W - 290, FB + 26, Color.FromArgb(200, 30, 30));
        }

        void DrawTeamStrip(Graphics g, List<Player> team, int startX, int startY, Color teamColor)
        {
            for (int i = 0; i < team.Count; i++)
            {
                Player p = team[i];
                int bx = startX + i * 70;
                bool isActiveHuman = (p.Side == Team.Blue && p.IsActive);

                SolidBrush boxBr = new SolidBrush(isActiveHuman
                    ? Color.FromArgb(80, teamColor.R, teamColor.G, teamColor.B)
                    : Color.FromArgb(30, 40, 55));
                g.FillRectangle(boxBr, bx, startY, 65, 38); boxBr.Dispose();

                Pen bPen = new Pen(isActiveHuman ? teamColor : Color.FromArgb(60, 80, 100),
                    isActiveHuman ? 2f : 1f);
                g.DrawRectangle(bPen, bx, startY, 65, 38); bPen.Dispose();

                Font pf = new Font("Segoe UI", 7.5f, FontStyle.Bold);
                SolidBrush pBr = new SolidBrush(teamColor);
                g.DrawString(p.Name, pf, pBr, bx + 3, startY + 2);
                pf.Dispose(); pBr.Dispose();

                string roleS = "";
                switch (p.Role)
                {
                    case Role.Defender: roleS = "Defender"; break;
                    case Role.Scorer: roleS = "Scorer"; break;
                    case Role.Captain: roleS = "Captain"; break;
                    case Role.Helper: roleS = "Helper"; break;
                }
                Font rf = new Font("Segoe UI", 6.5f);
                SolidBrush rfBr = new SolidBrush(Color.FromArgb(160, 160, 160));
                g.DrawString(roleS, rf, rfBr, bx + 3, startY + 17);
                rf.Dispose(); rfBr.Dispose();

                if (isActiveHuman)
                {
                    Font af = new Font("Segoe UI", 6f, FontStyle.Bold);
                    SolidBrush aBr = new SolidBrush(Color.FromArgb(255, 255, 100));
                    g.DrawString("YOU", af, aBr, bx + 3, startY + 27);
                    af.Dispose(); aBr.Dispose();
                }
            }
        }

        // ── Round result banner ───────────────────────────────────────────
        void DrawRoundResult(Graphics g)
        {
            SolidBrush ov = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            g.FillRectangle(ov, 0, 0, W, H); ov.Dispose();

            Font rf = new Font("Impact", 44f);
            DrawShadow(g, roundResultMsg, rf, Color.FromArgb(255, 220, 50), W / 2f, H / 2f - 60f, true);
            rf.Dispose();

            Font sf = new Font("Segoe UI", 16f);
            DrawShadow(g, "Next round starting...", sf, Color.FromArgb(180, 200, 200), W / 2f, H / 2f + 10f, true);
            sf.Dispose();
        }

        // ── Pause overlay ─────────────────────────────────────────────────
        void DrawPauseOverlay(Graphics g)
        {
            SolidBrush ov = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
            g.FillRectangle(ov, 0, 0, W, H); ov.Dispose();

            Font pf = new Font("Impact", 60f);
            DrawShadow(g, "PAUSED", pf, Color.FromArgb(255, 210, 50), W / 2f, 200f, true);
            pf.Dispose();

            Font sf = new Font("Segoe UI", 14f);
            DrawShadow(g, "P  =  Resume", sf, Color.White, W / 2f, 310f, true);
            DrawShadow(g, "Esc = Return to Menu", sf, Color.FromArgb(180, 180, 180), W / 2f, 345f, true);
            sf.Dispose();
        }

        // ── Game over screen ──────────────────────────────────────────────
        void DrawGameOver(Graphics g)
        {
            SolidBrush ov = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
            g.FillRectangle(ov, 0, 0, W, H); ov.Dispose();

            string winner;
            Color wc;
            if (blueRoundWins > redRoundWins) { winner = "BLUE TEAM WINS!"; wc = Color.FromArgb(80, 160, 255); }
            else if (redRoundWins > blueRoundWins) { winner = "RED TEAM WINS!"; wc = Color.FromArgb(255, 80, 80); }
            else { winner = "IT'S A DRAW!"; wc = Color.FromArgb(255, 220, 50); }

            Font wf = new Font("Impact", 52f);
            DrawShadow(g, winner, wf, wc, W / 2f, 140f, true);
            wf.Dispose();

            Font sf = new Font("Segoe UI", 16f, FontStyle.Bold);
            DrawShadow(g, "Blue Rounds: " + blueRoundWins + "     Red Rounds: " + redRoundWins,
                sf, Color.White, W / 2f, 230f, true);
            sf.Dispose();

            Font hf = new Font("Segoe UI", 12f);
            DrawShadow(g, "R = Play Again     Esc = Main Menu", hf, Color.FromArgb(180, 200, 200), W / 2f, 300f, true);
            hf.Dispose();
        }

        // ── Particles ─────────────────────────────────────────────────────
        void DrawParticles(Graphics g)
        {
            foreach (Particle p in parts)
            {
                int a = (int)(p.Alpha * 255f);
                if (a < 0) a = 0; if (a > 255) a = 255;
                SolidBrush pb = new SolidBrush(Color.FromArgb(a, p.Col));
                g.FillEllipse(pb, p.X - p.Size / 2f, p.Y - p.Size / 2f, p.Size, p.Size);
                pb.Dispose();
            }
        }

        // ── Shadow text helper ─────────────────────────────────────────────
        void DrawShadow(Graphics g, string txt, Font f, Color c, float x, float y, bool center = false)
        {
            SolidBrush sh = new SolidBrush(Color.FromArgb(130, 0, 0, 0));
            SolidBrush br = new SolidBrush(c);
            SizeF sz = g.MeasureString(txt, f);
            float ox = center ? x - sz.Width / 2f : x;
            g.DrawString(txt, f, sh, ox + 2f, y + 2f);
            g.DrawString(txt, f, br, ox, y);
            sh.Dispose(); br.Dispose();
        }

        // ================================================================
        //  HELPERS
        // ================================================================
        float PDist(Player p, float tx, float ty)
        {
            float dx = p.X - tx, dy = p.Y - ty;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        static float Clamp(float v, float lo, float hi)
        {
            if (v < lo) return lo;
            if (v > hi) return hi;
            return v;
        }

        // ================================================================
        //  INPUT
        // ================================================================
        void OnKeyDown(object sender, KeyEventArgs e)
        {
            // ── MENU INPUT ───────────────────────────────────────────────
            if (state == GameState.Menu)
            {
                if (menuMode == 0)
                {
                    // Page 1: Mode selection (Practice / Play)
                    if (e.KeyCode == Keys.Up) menuSel = (menuSel + 1) % 2;
                    if (e.KeyCode == Keys.Down) menuSel = (menuSel + 1) % 2;
                    if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
                    {
                        if (menuSel == 0)
                        {
                            // Practice Mode selected
                            aiLevel = Difficulty.Low;  // practice uses easy AI for inactive players
                            StartPractice();
                        }
                        else
                        {
                            // Play directly — go to difficulty page
                            menuMode = 1;
                            menuSel = 1;  // default Medium
                        }
                    }
                }
                else
                {
                    // Page 2: Difficulty selection
                    if (e.KeyCode == Keys.Up) menuSel = (menuSel + 2) % 3;
                    if (e.KeyCode == Keys.Down) menuSel = (menuSel + 1) % 3;
                    if (e.KeyCode == Keys.Escape) { menuMode = 0; menuSel = 1; }
                    if (e.KeyCode == Keys.Return || e.KeyCode == Keys.Space)
                    {
                        aiLevel = (Difficulty)menuSel;
                        menuMode = 0;
                        menuSel = 1;
                        StartGame();
                    }
                }
                return;
            }

            // ── PRACTICE INPUT ───────────────────────────────────────────
            if (state == GameState.Practice)
            {
                // ESC returns to main menu at ANY point during practice
                if (e.KeyCode == Keys.Escape)
                {
                    practiceActive = false;
                    practiceStep = 0;
                    practiceWaiting = true;
                    taskDone = false;
                    taskDoneTimer = 0;
                    menuMode = 0;
                    menuSel = 0;
                    state = GameState.Menu;
                    return;
                }

                if (practiceWaiting)
                {
                    if (e.KeyCode == Keys.Space)
                    {
                        practiceWaiting = false;
                        taskDone = false;
                        ball.X = W / 2f; ball.Y = (FT + FB) / 2f;
                        ball.VX = 0f; ball.VY = 0f;
                    }
                    return;
                }

                // Normal movement during active practice step
                switch (e.KeyCode)
                {
                    case Keys.Left: keyLeft = true; break;
                    case Keys.Right: keyRight = true; break;
                    case Keys.Up: keyUp = true; break;
                    case Keys.Down: keyDown = true; break;
                    case Keys.Space: keyHit = true; break;
                    case Keys.Tab:
                        activeBlueIndex = (activeBlueIndex + 1) % 4;
                        MarkActivePlayer();
                        break;
                }
                return;
            }

            // ── GAME OVER ────────────────────────────────────────────────
            if (state == GameState.GameOver)
            {
                if (e.KeyCode == Keys.R) { menuMode = 0; menuSel = 1; state = GameState.Menu; }
                if (e.KeyCode == Keys.Escape) { menuMode = 0; menuSel = 1; state = GameState.Menu; }
                return;
            }

            // ── PAUSED ───────────────────────────────────────────────────
            if (state == GameState.Paused)
            {
                if (e.KeyCode == Keys.P) state = GameState.Playing;
                if (e.KeyCode == Keys.Escape) { menuMode = 0; menuSel = 1; state = GameState.Menu; }
                return;
            }

            // ── PLAYING ──────────────────────────────────────────────────
            if (state == GameState.Playing)
            {
                switch (e.KeyCode)
                {
                    case Keys.Left: keyLeft = true; break;
                    case Keys.Right: keyRight = true; break;
                    case Keys.Up: keyUp = true; break;
                    case Keys.Down: keyDown = true; break;
                    case Keys.Space: keyHit = true; break;
                    case Keys.Tab:
                        activeBlueIndex = (activeBlueIndex + 1) % 4;
                        MarkActivePlayer();
                        break;
                    case Keys.P:
                        state = GameState.Paused;
                        break;
                }
            }
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: keyLeft = false; break;
                case Keys.Right: keyRight = false; break;
                case Keys.Up: keyUp = false; break;
                case Keys.Down: keyDown = false; break;
                case Keys.Space: keyHit = false; break;
            }
        }
    }
}