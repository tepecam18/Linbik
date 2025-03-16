FROM node:22-alpine

# Çalışma dizinini belirliyoruz
WORKDIR /app

# Paket tanımlama dosyalarını kopyalıyoruz
COPY package*.json ./

# RUN npm install -g npm@latest

# Bağımlılıkları yüklüyoruz
RUN npm install

# Tüm proje dosyalarını kopyalıyoruz
ADD . /app

# Uygulamayı derliyoruz
RUN npm run build

# Container’ın 3000 portunu expose ediyoruz
EXPOSE 3000

# Üretim modunda uygulamayı başlatıyoruz
CMD [ "npm", "start" ]
