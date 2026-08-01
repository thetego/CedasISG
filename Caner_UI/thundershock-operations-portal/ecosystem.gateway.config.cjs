module.exports = {
  apps: [{
    name: "cedas-playfab-gateway",
    script: "gateway/index.js",
    cwd: __dirname,
    instances: 1,
    exec_mode: "fork",
    autorestart: true,
    max_memory_restart: "500M",
    kill_timeout: 10000,
    listen_timeout: 10000,
    env: {
      NODE_ENV: "production",
      GATEWAY_HOST: "127.0.0.1",
      GATEWAY_PORT: "4180",
      PLAYFAB_TITLE_ID: "797DC",
    },
  }],
};
