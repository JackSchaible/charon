import {
  APIGatewayProxyHandler,
  APIGatewayProxyEvent,
  APIGatewayProxyResult,
} from "aws-lambda";
import { Pool } from "pg";

// Database types (from shared/types.ts)
interface User {
  Id: number;
  SteamId: string;
  CreatedAt: string;
  AvatarUrl: string;
  ProfileUrl: string;
  Username: string;
}

// Database connection (from shared/db.ts)
const pool = new Pool({
  connectionString: process.env.SQL_CONNECTION_STRING,
});

interface SteamUser {
  steamid: string;
  communityvisibilitystate: number;
  profilestate: number;
  personaname: string;
  profileurl: string;
  avatar: string;
  avatarmedium: string;
  avatarfull: string;
  avatarhash: string;
  lastlogoff: number;
  personastate: number;
  realname?: string;
  primaryclanid?: string;
  timecreated?: number;
  personastateflags?: number;
  loccountrycode?: string;
  locstatecode?: string;
  loccityid?: number;
}

interface SteamResponse {
  response: {
    players: SteamUser[];
  };
}

const STEAM_WEB_API_KEY = process.env.STEAM_WEB_API_KEY;
const STEAM_REALM = process.env.STEAM_REALM || "http://localhost:3000";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "Content-Type",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};

export const handler: APIGatewayProxyHandler = async (
  event: APIGatewayProxyEvent
): Promise<APIGatewayProxyResult> => {
  // Handle preflight requests
  if (event.httpMethod === "OPTIONS") {
    return {
      statusCode: 200,
      headers: corsHeaders,
      body: "",
    };
  }

  try {
    if (event.httpMethod === "GET" && event.path === "/auth/steam/login") {
      return handleSteamLogin(event);
    }

    if (event.httpMethod === "GET" && event.path === "/auth/steam/callback") {
      return await handleSteamCallback(event);
    }

    if (event.httpMethod === "POST" && event.path === "/auth/steam/verify") {
      return await handleSteamVerify(event);
    }

    return {
      statusCode: 404,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Not found" }),
    };
  } catch (error) {
    console.error("Error:", error);
    return {
      statusCode: 500,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Internal server error" }),
    };
  }
};

function handleSteamLogin(event: APIGatewayProxyEvent): APIGatewayProxyResult {
  const returnUrl =
    event.queryStringParameters?.returnUrl || `${STEAM_REALM}/auth/callback`;

  const steamLoginUrl =
    "https://steamcommunity.com/openid/login?" +
    new URLSearchParams({
      "openid.ns": "http://specs.openid.net/auth/2.0",
      "openid.mode": "checkid_setup",
      "openid.return_to": returnUrl,
      "openid.realm": STEAM_REALM,
      "openid.identity": "http://specs.openid.net/auth/2.0/identifier_select",
      "openid.claimed_id": "http://specs.openid.net/auth/2.0/identifier_select",
    }).toString();

  return {
    statusCode: 302,
    headers: {
      ...corsHeaders,
      Location: steamLoginUrl,
    },
    body: "",
  };
}

async function handleSteamCallback(
  event: APIGatewayProxyEvent
): Promise<APIGatewayProxyResult> {
  const params = event.queryStringParameters || {};

  // Verify the OpenID response
  const isValid = await verifySteamOpenId(params);

  if (!isValid) {
    return {
      statusCode: 400,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Invalid Steam authentication" }),
    };
  }

  // Extract Steam ID from the claimed_id
  const claimedId = params["openid.claimed_id"];
  const steamIdMatch = claimedId?.match(/\/id\/(\d+)$/);

  if (!steamIdMatch) {
    return {
      statusCode: 400,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Could not extract Steam ID" }),
    };
  }

  const steamId = steamIdMatch[1];

  try {
    // Check if user exists in database
    let user = await getUserBySteamId(steamId);

    if (!user) {
      // Fetch user data from Steam Web API
      const steamUserData = await getSteamUserData(steamId);

      if (!steamUserData) {
        return {
          statusCode: 400,
          headers: corsHeaders,
          body: JSON.stringify({ error: "Could not fetch Steam user data" }),
        };
      }

      // Create new user in database
      user = await createUser(steamId, steamUserData);
    }

    // Return user data (you might want to generate a JWT token here)
    return {
      statusCode: 200,
      headers: corsHeaders,
      body: JSON.stringify({ user }),
    };
  } catch (error) {
    console.error("Database error:", error);
    return {
      statusCode: 500,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Database error" }),
    };
  }
}

async function handleSteamVerify(
  event: APIGatewayProxyEvent
): Promise<APIGatewayProxyResult> {
  const body = event.body ? JSON.parse(event.body) : {};
  const { steamId } = body;

  if (!steamId) {
    return {
      statusCode: 400,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Steam ID required" }),
    };
  }

  try {
    const user = await getUserBySteamId(steamId);

    if (!user) {
      return {
        statusCode: 404,
        headers: corsHeaders,
        body: JSON.stringify({ error: "User not found" }),
      };
    }

    return {
      statusCode: 200,
      headers: corsHeaders,
      body: JSON.stringify({ user }),
    };
  } catch (error) {
    console.error("Database error:", error);
    return {
      statusCode: 500,
      headers: corsHeaders,
      body: JSON.stringify({ error: "Database error" }),
    };
  }
}

async function verifySteamOpenId(params: {
  [key: string]: string | undefined;
}): Promise<boolean> {
  try {
    const verificationParams = new URLSearchParams();

    for (const [key, value] of Object.entries(params)) {
      if (key.startsWith("openid.") && value) {
        verificationParams.append(key, value);
      }
    }

    verificationParams.set("openid.mode", "check_authentication");

    const response = await fetch("https://steamcommunity.com/openid/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: verificationParams.toString(),
    });

    const responseText = await response.text();
    return responseText.includes("is_valid:true");
  } catch (error) {
    console.error("Steam OpenID verification error:", error);
    return false;
  }
}

async function getSteamUserData(steamId: string): Promise<SteamUser | null> {
  if (!STEAM_WEB_API_KEY) {
    console.error("STEAM_WEB_API_KEY not configured");
    return null;
  }

  try {
    const response = await fetch(
      `https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=${STEAM_WEB_API_KEY}&steamids=${steamId}`
    );

    if (!response.ok) {
      throw new Error(`Steam API responded with status: ${response.status}`);
    }

    const data = (await response.json()) as SteamResponse;

    if (data.response.players.length === 0) {
      return null;
    }

    return data.response.players[0];
  } catch (error) {
    console.error("Error fetching Steam user data:", error);
    return null;
  }
}

async function getUserBySteamId(steamId: string): Promise<User | null> {
  const client = await pool.connect();

  try {
    const result = await client.query(
      "SELECT * FROM users WHERE steam_id = $1",
      [steamId]
    );

    if (result.rows.length === 0) {
      return null;
    }

    const row = result.rows[0];
    return {
      Id: row.id,
      SteamId: row.steam_id,
      CreatedAt: row.created_at,
      AvatarUrl: row.avatar_url,
      ProfileUrl: row.profile_url,
      Username: row.username,
    };
  } finally {
    client.release();
  }
}

async function createUser(
  steamId: string,
  steamUserData: SteamUser
): Promise<User> {
  const client = await pool.connect();

  try {
    const result = await client.query(
      `INSERT INTO users (steam_id, username, avatar_url, profile_url, created_at) 
       VALUES ($1, $2, $3, $4, NOW()) 
       RETURNING *`,
      [
        steamId,
        steamUserData.personaname,
        steamUserData.avatarfull,
        steamUserData.profileurl,
      ]
    );

    const row = result.rows[0];
    return {
      Id: row.id,
      SteamId: row.steam_id,
      CreatedAt: row.created_at,
      AvatarUrl: row.avatar_url,
      ProfileUrl: row.profile_url,
      Username: row.username,
    };
  } finally {
    client.release();
  }
}
