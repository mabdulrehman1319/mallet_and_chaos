using System;
using System.Data.SqlClient;

namespace HockeyHorseGame
{
    // DatabaseHelper handles all SQL Server operations for the POLO game.
    //
    // Usage:
    //   1. Set your server name in CONNECTION_STRING below
    //   2. Call DatabaseHelper.InsertMatch(...) when a match starts
    //   3. Call DatabaseHelper.InsertRound(...) when a round ends
    //   4. Call DatabaseHelper.InsertGoal(...) when a goal is scored
    //   5. Call DatabaseHelper.SavePlayerStats(...) when the game ends

    static class DatabaseHelper
    {
        // Connection string for the local SQL Server instance
        private const string CONNECTION_STRING =
            "Server=abdulrehman\\SQLEXPRESS;" +
            "Database=POLOGAME_DB;" +
            "Integrated Security=True;" +
            "TrustServerCertificate=True;";

        // Users
        // Call this at game startup to get or create a user.
        // Returns the user_id from the database.
        public static int GetOrCreateUser(string username)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    // Check if user already exists
                    string checkSql = "SELECT user_id FROM users WHERE username = @username";
                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        object result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            // User exists — update last_played and return their id
                            int existingId = Convert.ToInt32(result);
                            UpdateLastPlayed(existingId);
                            return existingId;
                        }
                    }

                    // User doesn't exist — insert new user
                    string insertSql = @"
                        INSERT INTO users (username, created_at, last_played)
                        VALUES (@username, GETDATE(), GETDATE());
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(insertSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("GetOrCreateUser", ex);
                return -1;
            }
        }

        static void UpdateLastPlayed(int userId)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = "UPDATE users SET last_played = GETDATE() WHERE user_id = @uid";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { LogError("UpdateLastPlayed", ex); }
        }

        // Matches
        // Call this in StartGame() — returns the new match_id
        public static int InsertMatch(int userId, string difficulty, int totalRounds)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO matches (user_id, difficulty, total_rounds, played_at)
                        VALUES (@uid, @diff, @rounds, GETDATE());
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        cmd.Parameters.AddWithValue("@diff", difficulty.ToLower());
                        cmd.Parameters.AddWithValue("@rounds", totalRounds);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("InsertMatch", ex);
                return -1;
            }
        }

        // Update round
        // Call this in EndRound() to fill in final scores and duration
        public static void UpdateRound(int roundId, int blueGoals, int redGoals,
                                       string winnerTeam, int durationSeconds)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = @"
                        UPDATE rounds
                        SET blue_goals       = @bg,
                            red_goals        = @rg,
                            winner_team      = @winner,
                            duration_seconds = @dur
                        WHERE round_id = @rid";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@bg", blueGoals);
                        cmd.Parameters.AddWithValue("@rg", redGoals);
                        cmd.Parameters.AddWithValue("@winner", winnerTeam.ToLower());
                        cmd.Parameters.AddWithValue("@dur", durationSeconds);
                        cmd.Parameters.AddWithValue("@rid", roundId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { LogError("UpdateRound", ex); }
        }

        // Call this in EndRound() or GameOver to set the winner
        public static void UpdateMatchWinner(int matchId, string winnerTeam)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = "UPDATE matches SET winner_team = @winner WHERE match_id = @mid";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@winner", winnerTeam.ToLower());
                        cmd.Parameters.AddWithValue("@mid", matchId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { LogError("UpdateMatchWinner", ex); }
        }

        // Rounds
        // Call this in EndRound() — returns the new round_id
        public static int InsertRound(int matchId, int roundNumber,
                                      int blueGoals, int redGoals,
                                      string winnerTeam, int durationSeconds)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO rounds
                            (match_id, round_number, blue_goals, red_goals, winner_team, duration_seconds)
                        VALUES
                            (@mid, @rnum, @bg, @rg, @winner, @dur);
                        SELECT SCOPE_IDENTITY();";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@mid", matchId);
                        cmd.Parameters.AddWithValue("@rnum", roundNumber);
                        cmd.Parameters.AddWithValue("@bg", blueGoals);
                        cmd.Parameters.AddWithValue("@rg", redGoals);
                        cmd.Parameters.AddWithValue("@winner", winnerTeam.ToLower());
                        cmd.Parameters.AddWithValue("@dur", durationSeconds);
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("InsertRound", ex);
                return -1;
            }
        }

        // Goal log
        // Call this in CheckGoals() every time a goal happens
        public static void InsertGoal(int roundId, string scoringTeam,
                                      string playerName, string playerRole,
                                      int goalMinute)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();
                    string sql = @"
                        INSERT INTO goal_log
                            (round_id, scoring_team, scoring_player_name, scoring_player_role, goal_minute)
                        VALUES
                            (@rid, @team, @pname, @prole, @min)";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@rid", roundId);
                        cmd.Parameters.AddWithValue("@team", scoringTeam.ToLower());
                        cmd.Parameters.AddWithValue("@pname", (object)playerName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@prole", (object)playerRole ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@min", goalMinute);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex) { LogError("InsertGoal", ex); }
        }

        // Player stats
        // Call this in GameOver — updates cumulative stats for each player
        public static void SavePlayerStats(int userId, string playerName,
                                           string playerRole, int goalsThisMatch,
                                           int hitsThisMatch)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
                {
                    conn.Open();

                    // Check if a stats row already exists for this user + player
                    string checkSql = @"
                        SELECT stat_id FROM player_stats
                        WHERE user_id = @uid AND player_name = @pname";

                    using (SqlCommand cmd = new SqlCommand(checkSql, conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        cmd.Parameters.AddWithValue("@pname", playerName);
                        object result = cmd.ExecuteScalar();

                        if (result != null)
                        {
                            // Row exists — add to existing totals
                            string updateSql = @"
                                UPDATE player_stats
                                SET total_goals    = total_goals    + @goals,
                                    total_hits     = total_hits     + @hits,
                                    matches_played = matches_played + 1
                                WHERE user_id = @uid AND player_name = @pname";

                            using (SqlCommand upd = new SqlCommand(updateSql, conn))
                            {
                                upd.Parameters.AddWithValue("@goals", goalsThisMatch);
                                upd.Parameters.AddWithValue("@hits", hitsThisMatch);
                                upd.Parameters.AddWithValue("@uid", userId);
                                upd.Parameters.AddWithValue("@pname", playerName);
                                upd.ExecuteNonQuery();
                            }
                        }
                        else
                        {
                            // No row yet — insert fresh stats
                            string insertSql = @"
                                INSERT INTO player_stats
                                    (user_id, player_name, player_role, total_goals, total_hits, matches_played)
                                VALUES
                                    (@uid, @pname, @prole, @goals, @hits, 1)";

                            using (SqlCommand ins = new SqlCommand(insertSql, conn))
                            {
                                ins.Parameters.AddWithValue("@uid", userId);
                                ins.Parameters.AddWithValue("@pname", playerName);
                                ins.Parameters.AddWithValue("@prole", playerRole.ToLower());
                                ins.Parameters.AddWithValue("@goals", goalsThisMatch);
                                ins.Parameters.AddWithValue("@hits", hitsThisMatch);
                                ins.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("SavePlayerStats", ex); }
        }

        // Error logging — shows a message box if something goes wrong
        static void LogError(string method, Exception ex)
        {
            System.Windows.Forms.MessageBox.Show(
                "Database error in " + method + ":\n" + ex.Message,
                "DB Error",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }
    }
}
