/**
 * Build a string in a format of query param to the endpoint
 * @param {*} queryparams a JSON object with key as query name and value as query value
 * @example {"key1": "value1", "key2": "value2"}
 * @returns string a string like key1=value&key2=value2
 */
export function buildQueryParametersForEndpoint(queryparams) {
  let query = "?";

  Object.keys(queryparams).forEach((key) => {
    if (Array.isArray(queryparams[key])) {
      queryparams[key].forEach((value) => {
        query += `${key}=${value}&`;
      });
    } else {
      query += `${key}=${queryparams[key]}&`;
    }
  });

  query = query.slice(0, -1);
  
  return query;
}

export function buildHeaderWithBearer(token) {
  return {
    headers: {
      Authorization: `Bearer ${token}`
    }
  };
}

export function buildHeaderWithBasic(token) {
  return {
    headers: {
      Authorization: `Basic ${token}`
    }
  };
}

export function buildHeaderWithBearerAndContentType(token) {
  return {
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json"
    }
  };
}

export function buildHeaderWithContentType(contentType) {
  return {
    headers: {
      "Content-Type": contentType
    }
  };
}
