FROM node:20-alpine
RUN apk add --no-cache git
WORKDIR /usr/server/app

RUN git clone https://github.com/paulmfischer/MediaSet.git
WORKDIR /usr/server/app/MediaSet/MediaSet.Remix

RUN npm install

COPY ./ .

ARG APP_VERSION=0.0.0-local
ENV VITE_APP_VERSION=${APP_VERSION}

RUN npm run build
ENV NODE_ENV=production

CMD ["npm", "run", "start"]