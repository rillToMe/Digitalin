import api from "./api";
import type { SearchGamesResponse, SearchGamesParams } from "../api/index";

/**
 * Search service — all game search API communication goes through here.
 */
export const searchService = {
  /**
   * Search game brands by keyword.
   * GET /search/games?q={keyword}&limit={limit}
   */
  searchGames: (params: SearchGamesParams, signal?: AbortSignal) =>
    api
      .get<SearchGamesResponse>("/search/games", { params, signal })
      .then((r) => r.data),
};
