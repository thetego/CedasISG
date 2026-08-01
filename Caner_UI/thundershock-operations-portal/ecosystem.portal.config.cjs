module.exports = {
  apps: [{
    name: "cedas-panel",
    script: "server/index.js",
    cwd: __dirname,
    instances: 1,
    exec_mode: "fork",
    autorestart: true,
    max_memory_restart: "350M",
    kill_timeout: 10000,
    listen_timeout: 10000,
    env: {
      NODE_ENV: "production",
      HOST: "127.0.0.1",
      PORT: "3040",
    },
  }],
};
